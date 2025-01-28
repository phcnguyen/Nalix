﻿using Notio.Logging;
using Notio.Web.Authentication;
using Notio.Web.Http;
using Notio.Web.Internal;
using Notio.Web.Routing;
using Notio.Web.Sessions;
using Notio.Web.Utilities;
using Notio.Web.WebSockets;
using Notio.Web.WebSockets.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Web.Net.Internal;

internal sealed class SystemHttpContext : IHttpContextImpl
{
    private readonly System.Net.HttpListenerContext _context;

    private readonly TimeKeeper _ageKeeper = new();

    private readonly Stack<Action<IHttpContext>> _closeCallbacks = new();

    private bool _closed;

    public SystemHttpContext(System.Net.HttpListenerContext context)
    {
        _context = context;

        Request = new SystemHttpRequest(_context);
        User = _context.User ?? Auth.NoUser;
        Response = new SystemHttpResponse(_context);
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

    public IHttpRequest Request { get; }

    public RouteMatch Route { get; set; }

    public string RequestedPath => Route.SubPath ?? string.Empty; // It will never be empty, because modules are matched via base routes - this is just to silence a warning.

    public IHttpResponse Response { get; }

    public IPrincipal User { get; set; }

    public ISessionProxy Session { get; set; }

    public bool SupportCompressedRequests { get; set; }

    public IDictionary<object, object> Items { get; } = new Dictionary<object, object>();

    public bool IsHandled { get; private set; }

    public MimeTypeProviderStack MimeTypeProviders { get; } = new MimeTypeProviderStack();

    public void SetHandled()
    {
        IsHandled = true;
    }

    public void OnClose(Action<IHttpContext> callback)
    {
        if (_closed)
        {
            throw new InvalidOperationException("HTTP context has already been closed.");
        }

        _closeCallbacks.Push(Validate.NotNull(nameof(callback), callback));
    }

    public async Task<IWebSocketContext> AcceptWebSocketAsync(
        IEnumerable<string> requestedProtocols,
        string acceptedProtocol,
        int receiveBufferSize,
        TimeSpan keepAliveInterval,
        CancellationToken cancellationToken)
    {
        System.Net.WebSockets.HttpListenerWebSocketContext context = await _context.AcceptWebSocketAsync(
            acceptedProtocol.NullIfEmpty(), // Empty string would throw; use null to signify "no subprotocol" here.
            receiveBufferSize,
            keepAliveInterval)
            .ConfigureAwait(false);
        return new WebSocketContext(this, context.SecWebSocketVersion, requestedProtocols, acceptedProtocol, new SystemWebSocket(context.WebSocket), cancellationToken);
    }

    public void Close()
    {
        _closed = true;

        // Always close the response stream no matter what.
        Response.Close();

        foreach (Action<IHttpContext> callback in _closeCallbacks)
        {
            try
            {
                callback(this);
            }
            catch (Exception e)
            {
                e.Log("HTTP context", "[Id] Exception thrown by a HTTP context close callback.");
            }
        }
    }

    public string GetMimeType(string extension)
    {
        return MimeTypeProviders.GetMimeType(extension);
    }

    public bool TryDetermineCompression(string mimeType, out bool preferCompression)
    {
        return MimeTypeProviders.TryDetermineCompression(mimeType, out preferCompression);
    }
}