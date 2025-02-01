﻿using Notio.Network.Web.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Notio.Network.Web.Net.Internal;

internal sealed class EndPointListener : IDisposable
{
    private readonly Dictionary<HttpConnection, HttpConnection> _unregistered;
    private readonly IPEndPoint _endpoint;
    private readonly Socket _sock;
    private Dictionary<ListenerPrefix, HttpListener> _prefixes;
    private List<ListenerPrefix>? _unhandled; // unhandled; host = '*'
    private List<ListenerPrefix>? _all; //  all;  host = '+

    public EndPointListener(HttpListener listener, IPAddress address, int port, bool secure)
    {
        Listener = listener;
        Secure = secure;
        _endpoint = new IPEndPoint(address, port);
        _sock = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && EndPointManager.UseIpv6)
        {
            _sock.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        }

        _sock.Bind(_endpoint);
        _sock.Listen(500);
        SocketAsyncEventArgs args = new() { UserToken = this };
        args.Completed += OnAccept!;
        Socket? dummy = null;
        Accept(_sock, args, ref dummy);
        _prefixes = [];
        _unregistered = [];
    }

    internal HttpListener Listener { get; }

    internal bool Secure { get; }

    public bool BindContext(HttpListenerContext context)
    {
        IHttpRequest req = context.Request;
        HttpListener? listener = SearchListener(req.Url, out ListenerPrefix? prefix);

        if (listener == null)
        {
            return false;
        }

        context.Listener = listener;
        context.Connection.Prefix = prefix;
        return true;
    }

    public static void UnbindContext(HttpListenerContext context)
    {
        context.Listener?.UnregisterContext(context);
    }

    public void Dispose()
    {
        _sock.Dispose();
        List<HttpConnection> connections;

        lock (_unregistered)
        {
            // Clone the list because RemoveConnection can be called from Close
            connections = new List<HttpConnection>(_unregistered.Keys);
            _unregistered.Clear();
        }

        foreach (HttpConnection c in connections)
        {
            c.Dispose();
        }
    }

    public void AddPrefix(ListenerPrefix prefix, HttpListener listener)
    {
        List<ListenerPrefix>? current;
        List<ListenerPrefix> future;

        if (prefix.Host == "*")
        {
            do
            {
                current = _unhandled;

                // TODO: Should we clone the items?
                future = current?.ToList() ?? [];
                prefix.Listener = listener;
                AddSpecial(future, prefix);
            }
            while (Interlocked.CompareExchange(ref _unhandled, future, current) != current);

            return;
        }

        if (prefix.Host == "+")
        {
            do
            {
                current = _all;
                future = current?.ToList() ?? [];
                prefix.Listener = listener;
                AddSpecial(future, prefix);
            }
            while (Interlocked.CompareExchange(ref _all, future, current) != current);
            return;
        }

        Dictionary<ListenerPrefix, HttpListener> prefs, p2;

        do
        {
            prefs = _prefixes;
            if (prefs.ContainsKey(prefix))
            {
                if (prefs[prefix] != listener)
                {
                    throw new HttpListenerException(400, $"There is another listener for {prefix}");
                }

                return;
            }

            p2 = prefs.ToDictionary(x => x.Key, x => x.Value);
            p2[prefix] = listener;
        }
        while (Interlocked.CompareExchange(ref _prefixes, p2, prefs) != prefs);
    }

    public void RemovePrefix(ListenerPrefix prefix)
    {
        List<ListenerPrefix>? current;
        List<ListenerPrefix> future;

        if (prefix.Host == "*")
        {
            do
            {
                current = _unhandled;
                future = current?.ToList() ?? [];
                if (!RemoveSpecial(future, prefix))
                {
                    break; // Prefix not found
                }
            }
            while (Interlocked.CompareExchange(ref _unhandled, future, current) != current);

            CheckIfRemove();
            return;
        }

        if (prefix.Host == "+")
        {
            do
            {
                current = _all;
                future = current?.ToList() ?? [];
                if (!RemoveSpecial(future, prefix))
                {
                    break; // Prefix not found
                }
            }
            while (Interlocked.CompareExchange(ref _all, future, current) != current);

            CheckIfRemove();
            return;
        }

        Dictionary<ListenerPrefix, HttpListener> prefs, p2;

        do
        {
            prefs = _prefixes;
            ListenerPrefix? prefixKey = _prefixes.Keys.FirstOrDefault(p => p.Path == prefix.Path);

            if (prefixKey is null)
            {
                break;
            }

            p2 = prefs.ToDictionary(x => x.Key, x => x.Value);
            _ = p2.Remove(prefixKey);
        }
        while (Interlocked.CompareExchange(ref _prefixes, p2, prefs) != prefs);

        CheckIfRemove();
    }

    internal void RemoveConnection(HttpConnection conn)
    {
        lock (_unregistered)
        {
            _ = _unregistered.Remove(conn);
        }
    }

    private static void Accept(Socket socket, SocketAsyncEventArgs e, ref Socket? accepted)
    {
        e.AcceptSocket = null;
        bool acceptPending;

        try
        {
            acceptPending = socket.AcceptAsync(e);
        }
        catch
        {
            try
            {
                accepted?.Dispose();
            }
            catch
            {
                // ignored
            }

            accepted = null;
            return;
        }

        if (!acceptPending)
        {
            ProcessAccept(e);
        }
    }

    private static void ProcessAccept(SocketAsyncEventArgs args)
    {
        Socket? accepted = null;
        if (args.SocketError == SocketError.Success)
        {
            accepted = args.AcceptSocket;
        }

        if (args.UserToken is not EndPointListener epl)
            return;

        Accept(epl._sock, args, ref accepted);
        if (accepted == null)
        {
            return;
        }

        if (epl.Secure && epl.Listener.Certificate == null)
        {
            accepted.Dispose();
            return;
        }

        HttpConnection conn;
        try
        {
            conn = new HttpConnection(accepted, epl);
        }
        catch
        {
            return;
        }

        lock (epl._unregistered)
        {
            epl._unregistered[conn] = conn;
        }

        _ = conn.BeginReadRequest();
    }

    private static void OnAccept(object sender, SocketAsyncEventArgs e)
    {
        ProcessAccept(e);
    }

    private static HttpListener? MatchFromList(string path, List<ListenerPrefix>? list, out ListenerPrefix? prefix)
    {
        prefix = null;
        if (list == null)
        {
            return null;
        }

        HttpListener? bestMatch = null;
        int bestLength = -1;

        foreach (ListenerPrefix p in list)
        {
            if (p.Path.Length < bestLength || !path.StartsWith(p.Path, StringComparison.Ordinal))
            {
                continue;
            }

            bestLength = p.Path.Length;
            bestMatch = p.Listener;
            prefix = p;
        }

        return bestMatch;
    }

    private static void AddSpecial(ICollection<ListenerPrefix> coll, ListenerPrefix prefix)
    {
        if (coll == null)
        {
            return;
        }

        if (coll.Any(p => p.Path == prefix.Path))
        {
            throw new HttpListenerException(400, "Prefix already in use.");
        }

        coll.Add(prefix);
    }

    private static bool RemoveSpecial(IList<ListenerPrefix> coll, ListenerPrefix prefix)
    {
        if (coll == null)
        {
            return false;
        }

        int c = coll.Count;
        for (int i = 0; i < c; i++)
        {
            if (coll[i].Path != prefix.Path)
            {
                continue;
            }

            coll.RemoveAt(i);
            return true;
        }

        return false;
    }

    private HttpListener? SearchListener(Uri uri, out ListenerPrefix? prefix)
    {
        prefix = null;
        if (uri == null)
        {
            return null;
        }

        string host = uri.Host;
        int port = uri.Port;
        string path = WebUtility.UrlDecode(uri.AbsolutePath);
        string pathSlash = path[^1] == '/' ? path : path + "/";

        HttpListener? bestMatch = null;
        int bestLength = -1;

        if (!string.IsNullOrEmpty(host))
        {
            Dictionary<ListenerPrefix, HttpListener> result = _prefixes;

            foreach (ListenerPrefix p in result.Keys)
            {
                if (p.Path.Length < bestLength)
                {
                    continue;
                }

                if (p.Host != host || p.Port != port)
                {
                    continue;
                }

                if (!path.StartsWith(p.Path, StringComparison.Ordinal) && !pathSlash.StartsWith(p.Path, StringComparison.Ordinal))
                {
                    continue;
                }

                bestLength = p.Path.Length;
                bestMatch = result[p];
                prefix = p;
            }

            if (bestLength != -1)
            {
                return bestMatch;
            }
        }

        List<ListenerPrefix>? list = _unhandled;
        bestMatch = MatchFromList(path, list, out prefix);
        if (path != pathSlash && bestMatch == null)
        {
            bestMatch = MatchFromList(pathSlash, list, out prefix);
        }

        if (bestMatch != null)
        {
            return bestMatch;
        }

        list = _all;
        bestMatch = MatchFromList(path, list, out prefix);
        if (path != pathSlash && bestMatch == null)
        {
            bestMatch = MatchFromList(pathSlash, list, out prefix);
        }

        return bestMatch;
    }

    private void CheckIfRemove()
    {
        if (_prefixes.Count > 0)
        {
            return;
        }

        List<ListenerPrefix>? list = _unhandled;
        if (list != null && list.Count > 0)
        {
            return;
        }

        list = _all;
        if (list != null && list.Count > 0)
        {
            return;
        }

        EndPointManager.RemoveEndPoint(this, _endpoint);
    }
}