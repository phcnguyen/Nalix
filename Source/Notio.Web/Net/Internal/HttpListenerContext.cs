﻿using Notio.Utilities;
using Notio.Web.Authentication;
using Notio.Web.Http;
using Notio.Web.Internal;
using Notio.Web.Routing;
using Notio.Web.Sessions;
using Notio.Web.Utilities;
using Notio.Web.WebSockets;
using Notio.Web.WebSockets.Internal;
using Swan.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Web.Net.Internal;

// Provides access to the request and response objects used by the HttpListener class.
internal sealed class HttpListenerContext : IHttpContextImpl
{
    private readonly Lazy<IDictionary<object, object>> _items = new(() => new Dictionary<object, object>(), true);

    private readonly TimeKeeper _ageKeeper = new();

    private readonly Stack<Action<IHttpContext>> _closeCallbacks = new();

    private bool _closed;

    internal HttpListenerContext(HttpConnection cnc)
    {
        Connection = cnc;
        HttpListenerRequest = new HttpListenerRequest(this);
        User = Auth.NoUser;
        HttpListenerResponse = new HttpListenerResponse(this);
        Id = UniqueIdGenerator.GetNext();
        LocalEndPoint = Request.LocalEndPoint;
        RemoteEndPoint = Request.RemoteEndPoint;
        Route = RouteMatch.None;
        Session = SessionProxy.None;
    }

    public string Id { get; }

    public CancellationToken CancellationToken { get; set; }

    public long Age => _ageKeeper.ElapsedTime;

    public IPEndPoint LocalEndPoint { get; }

    public IPEndPoint RemoteEndPoint { get; }

    public IHttpRequest Request => HttpListenerRequest;

    public RouteMatch Route { get; set; }

    public string RequestedPath => Route.SubPath ?? string.Empty; // It will never be empty, because modules are matched via base routes - this is just to silence a warning.

    public IHttpResponse Response => HttpListenerResponse;

    public IPrincipal User { get; set; }

    public ISessionProxy Session { get; set; }

    public bool SupportCompressedRequests { get; set; }

    public IDictionary<object, object> Items => _items.Value;

    public bool IsHandled { get; private set; }

    public MimeTypeProviderStack MimeTypeProviders { get; } = new MimeTypeProviderStack();

    internal HttpListenerRequest HttpListenerRequest { get; }

    internal HttpListenerResponse HttpListenerResponse { get; }

    internal HttpListener? Listener { get; set; }

    internal HttpConnection Connection { get; }

    public void SetHandled() => IsHandled = true;

    public void OnClose(Action<IHttpContext> callback)
    {
        if (_closed)
        {
            throw new InvalidOperationException("HTTP context has already been closed.");
        }

        _closeCallbacks.Push(Validate.NotNull(nameof(callback), callback));
    }

    public void Close()
    {
        _closed = true;

        // Always close the response stream no matter what.
        Response.Close();

        foreach (var callback in _closeCallbacks)
        {
            try
            {
                callback(this);
            }
            catch (Exception e)
            {
                e.Log("HTTP context", $"[{Id}] Exception thrown by a HTTP context close callback.");
            }
        }
    }

    public async Task<IWebSocketContext> AcceptWebSocketAsync(
        IEnumerable<string> requestedProtocols,
        string acceptedProtocol,
        int receiveBufferSize,
        TimeSpan keepAliveInterval,
        CancellationToken cancellationToken)
    {
        var webSocket = await WebSocket.AcceptAsync(this, acceptedProtocol).ConfigureAwait(false);
        return new WebSocketContext(this, WebSocket.SupportedVersion, requestedProtocols, acceptedProtocol, webSocket, cancellationToken);
    }

    public string GetMimeType(string extension)
        => MimeTypeProviders.GetMimeType(extension);

    public bool TryDetermineCompression(string mimeType, out bool preferCompression)
        => MimeTypeProviders.TryDetermineCompression(mimeType, out preferCompression);
}