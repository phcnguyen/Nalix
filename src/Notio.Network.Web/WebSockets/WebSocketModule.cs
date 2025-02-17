﻿using Notio.Network.Web.Enums;
using Notio.Network.Web.Http;
using Notio.Network.Web.Http.Exceptions;
using Notio.Network.Web.Http.Extensions;
using Notio.Network.Web.Utilities;
using Notio.Network.Web.WebModule;
using Notio.Network.Web.WebSockets.Internal;
using Notio.Shared.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Notio.Shared.Extensions;
using Notio.Common.Logging;

namespace Notio.Network.Web.WebSockets;

/// <summary>
/// A base class for modules that handle WebSocket connections.
/// </summary>
/// <remarks>
/// <para>Each WebSocket server has a list of WebSocket subprotocols it can accept.</para>
/// <para>When a client initiates a WebSocket opening handshake:</para>
/// <list type="bullet">
/// <item><description>if the list of accepted subprotocols is empty,
/// the connection is accepted only if no <c>SecWebSocketProtocol</c>
/// header is present in the request;</description></item>
/// <item><description>if the list of accepted subprotocols is not empty,
/// the connection is accepted only if one or more <c>SecWebSocketProtocol</c>
/// headers are present in the request and one of them specifies one
/// of the subprotocols in the list. The first subprotocol specified by the client
/// that is also present in the module's list is then specified in the
/// handshake response.</description></item>
/// </list>
/// If a connection is not accepted because of a subprotocol mismatch,
/// a <c>400 Bad Request</c> response is sent back to the client. The response
/// contains one or more <c>SecWebSocketProtocol</c> headers that specify
/// the list of accepted subprotocols (if any).
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="WebSocketModule" /> class.
/// </remarks>
/// <param name="urlPath">The URL path of the WebSocket endpoint to serve.</param>
/// <param name="enableConnectionWatchdog">If set to <see langword="true"/>,
/// contexts representing closed connections will automatically be purged
/// from <see cref="ActiveContexts"/> every 30 seconds..</param>
public abstract class WebSocketModule(string urlPath, bool enableConnectionWatchdog) : WebModuleBase(urlPath), IDisposable
{
    private const int ReceiveBufferSize = 2048;

    private readonly bool _enableConnectionWatchdog = enableConnectionWatchdog;
    private readonly List<string> _protocols = [];
    private readonly ConcurrentDictionary<string, IWebSocketContext> _contexts = new();
    private bool _isDisposing;
    private int _maxMessageSize;
    private TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(30);
    private Encoding _encoding = Encoding.UTF8;
    private PeriodicTask? _connectionWatchdog;

    /// <inheritdoc />
    public sealed override bool IsFinalHandler => true;

    /// <summary>
    /// <para>Gets or sets the maximum size of a received message.
    /// If a message exceeding the maximum size is received from a client,
    /// the connection is closed automatically.</para>
    /// <para>The default value is 0, which disables message size checking.</para>
    /// </summary>
    protected int MaxMessageSize
    {
        get => _maxMessageSize;
        set
        {
            EnsureConfigurationNotLocked();
            _maxMessageSize = Math.Max(value, 0);
        }
    }

    /// <summary>
    /// Gets or sets the keep-alive interval for the WebSocket connection.
    /// The default is 30 seconds.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">This property is being set to a value
    /// that is too small to be acceptable.</exception>
    protected TimeSpan KeepAliveInterval
    {
        get => _keepAliveInterval;
        set
        {
            EnsureConfigurationNotLocked();
            if (value != Timeout.InfiniteTimeSpan && value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The specified keep-alive interval is too small.");
            }

            _keepAliveInterval = value;
        }
    }

    /// <summary>
    /// Gets the <see cref="Encoding"/> used by the <see cref="SendAsync(IWebSocketContext,string)"/> method
    /// to send a string. The default is <see cref="Encoding.UTF8"/> per the WebSocket specification.
    /// </summary>
    /// <exception cref="ArgumentNullException">This property is being set to <see langword="null"/>.</exception>
    protected Encoding Encoding
    {
        get => _encoding;
        set
        {
            EnsureConfigurationNotLocked();
            _encoding = Validate.NotNull(nameof(value), value);
        }
    }

    /// <summary>
    /// Gets a list of <see cref="IWebSocketContext"/> interfaces
    /// representing the currently connected clients.
    /// </summary>
    protected IReadOnlyList<IWebSocketContext> ActiveContexts
    {
        get
        {
            // ConcurrentDictionary<TKey,TValue>.Values, although declared as ICollection<TValue>,
            // will probably return a ReadOnlyCollection<TValue>, which implements IReadOnlyList<TValue>:
            // https://referencesource.microsoft.com/#mscorlib/system/Collections/Concurrent/ConcurrentDictionary.cs,fe55c11912af21d2
            // https://github.com/dotnet/corefx/blob/master/src/System.Collections.Concurrent/src/System/Collections/Concurrent/ConcurrentDictionary.cs#L1990
            // https://github.com/mono/mono/blob/master/mcs/class/referencesource/mscorlib/system/collections/Concurrent/ConcurrentDictionary.cs#L1961
            // However there is no formal guarantee, so be ready to convert to a list, just in case.
            ICollection<IWebSocketContext> values = _contexts.Values;
            return values is IReadOnlyList<IWebSocketContext> list
                ? list
                : [.. values];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected sealed override async Task OnRequestAsync(IHttpContext context)
    {
        // The WebSocket endpoint must match exactly, giving a RequestedPath of "/".
        // In all other cases the path is longer, so there's no need to compare strings here.
        if (context.RequestedPath.Length > 1)
        {
            return;
        }

        bool acceptConnection;
        string acceptedProtocol;
        string[] requestedProtocols = context.Request.Headers
            .GetValues(HttpHeaderNames.SecWebSocketProtocol)?
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray() ?? [];

        if (_protocols.Count > 0)
        {
            acceptedProtocol = requestedProtocols.FirstOrDefault(_protocols.Contains) ?? string.Empty;
            acceptConnection = acceptedProtocol.Length > 0;
        }
        else
        {
            acceptedProtocol = string.Empty;
            acceptConnection = requestedProtocols.Length == 0;
        }

        if (!acceptConnection)
        {
            $"{BaseRoute} - Rejecting WebSocket connection: no subprotocol was accepted.".Debug(nameof(WebSocketModule));
            foreach (string protocol in _protocols)
            {
                context.Response.Headers.Add(HttpHeaderNames.SecWebSocketProtocol, protocol);
            }

            // Not throwing a HTTP exception here because a WebSocket client
            // does not care about nice, formatted messages.
            context.Response.SetEmptyResponse((int)HttpStatusCode.BadRequest);
            return;
        }

        IHttpContextImpl contextImpl = context.GetImplementation();
        $"{BaseRoute} - Accepting WebSocket connection with subprotocol \"{acceptedProtocol}\"".Debug(nameof(WebSocketModule));
        IWebSocketContext webSocketContext = await contextImpl.AcceptWebSocketAsync(
                requestedProtocols,
                acceptedProtocol,
                ReceiveBufferSize,
                KeepAliveInterval,
                context.CancellationToken).ConfigureAwait(false);

        PurgeDisconnectedContexts();
        _ = _contexts.TryAdd(webSocketContext.Id, webSocketContext);

        $"{BaseRoute} - WebSocket connection accepted - There are now {_contexts.Count} sockets connected."
            .Debug(nameof(WebSocketModule));

        await OnClientConnectedAsync(webSocketContext).ConfigureAwait(false);

        try
        {
            if (webSocketContext.WebSocket is SystemWebSocket systemWebSocket)
            {
                await ProcessSystemContext(
                        webSocketContext,
                        systemWebSocket.UnderlyingWebSocket,
                        context.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ProcessNotioContext(webSocketContext, context.CancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            ex.Log(nameof(WebSocketModule));
        }
        finally
        {
            // once the loop is completed or connection aborted, remove the WebSocket
            RemoveWebSocket(webSocketContext);
        }
    }

    /// <inheritdoc />
    protected override void OnStart(CancellationToken cancellationToken)
    {
        if (_enableConnectionWatchdog)
        {
            _connectionWatchdog = new PeriodicTask(
                TimeSpan.FromSeconds(30),
                _ =>
                {
                    PurgeDisconnectedContexts();
                    return Task.CompletedTask;
                },
                cancellationToken);
        }
    }

    /// <summary>
    /// Adds a WebSocket subprotocol to the list of protocols supported by a <see cref="WebSocketModule"/>.
    /// </summary>
    /// <param name="protocol">The protocol name to add to the list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="protocol"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <para><paramref name="protocol"/> contains one or more invalid characters, as defined
    /// in <see href="https://tools.ietf.org/html/rfc6455#section-4.3">RFC6455, Section 4.3</see>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="protocol"/> is already in the list of supported protocols.</para>
    /// </exception>
    /// <exception cref="InvalidOperationException">The <see cref="WebSocketModule"/> has already been started.</exception>
    /// <seealso cref="Validate.Rfc2616Token"/>
    /// <seealso cref="AddProtocols(IEnumerable{string})"/>
    /// <seealso cref="AddProtocols(string[])"/>
    protected void AddProtocol(string protocol)
    {
        protocol = Validate.Rfc2616Token(nameof(protocol), protocol);

        EnsureConfigurationNotLocked();

        if (_protocols.Contains(protocol))
        {
            throw new ArgumentException("Duplicate WebSocket protocol name.", nameof(protocol));
        }

        _protocols.Add(protocol);
    }

    /// <summary>
    /// Adds one or more WebSocket subprotocols to the list of protocols supported by a <see cref="WebSocketModule"/>.
    /// </summary>
    /// <param name="protocols">The protocol names to add to the list.</param>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="protocols"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para>One or more of the strings in <paramref name="protocols"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <para>One or more of the strings in <paramref name="protocols"/>
    /// contains one or more invalid characters, as defined
    /// in <see href="https://tools.ietf.org/html/rfc6455#section-4.3">RFC6455, Section 4.3</see>.</para>
    /// <para>- or -</para>
    /// <para>One or more of the strings in <paramref name="protocols"/>
    /// is already in the list of supported protocols.</para>
    /// </exception>
    /// <exception cref="InvalidOperationException">The <see cref="WebSocketModule"/> has already been started.</exception>
    /// <remarks>
    /// <para>This method enumerates <paramref name="protocols"/> just once; hence, if an exception is thrown
    /// because one of the specified protocols is <see langword="null"/> or contains invalid characters,
    /// any preceding protocol is added to the list of supported protocols.</para>
    /// </remarks>
    /// <seealso cref="Validate.Rfc2616Token"/>
    /// <seealso cref="AddProtocol"/>
    /// <seealso cref="AddProtocols(string[])"/>
    protected void AddProtocols(IEnumerable<string> protocols)
    {
        protocols = Validate.NotNull(nameof(protocols), protocols);

        EnsureConfigurationNotLocked();

        foreach (string? protocol in protocols.Select(p => Validate.Rfc2616Token(nameof(protocols), p)))
        {
            if (_protocols.Contains(protocol))
            {
                throw new ArgumentException("Duplicate WebSocket protocol name.", nameof(protocols));
            }

            _protocols.Add(protocol);
        }
    }

    /// <summary>
    /// Adds one or more WebSocket subprotocols to the list of protocols supported by a <see cref="WebSocketModule"/>.
    /// </summary>
    /// <param name="protocols">The protocol names to add to the list.</param>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="protocols"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para>One or more of the strings in <paramref name="protocols"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <para>One or more of the strings in <paramref name="protocols"/>
    /// contains one or more invalid characters, as defined
    /// in <see href="https://tools.ietf.org/html/rfc6455#section-4.3">RFC6455, Section 4.3</see>.</para>
    /// <para>- or -</para>
    /// <para>One or more of the strings in <paramref name="protocols"/>
    /// is already in the list of supported protocols.</para>
    /// </exception>
    /// <exception cref="InvalidOperationException">The <see cref="WebSocketModule"/> has already been started.</exception>
    /// <remarks>
    /// <para>This method performs validation checks on all specified <paramref name="protocols"/> before adding them
    /// to the list of supported protocols; hence, if an exception is thrown
    /// because one of the specified protocols is <see langword="null"/> or contains invalid characters,
    /// none of the specified protocol names are added to the list.</para>
    /// </remarks>
    /// <seealso cref="Validate.Rfc2616Token"/>
    /// <seealso cref="AddProtocol"/>
    /// <seealso cref="AddProtocols(IEnumerable{string})"/>
    protected void AddProtocols(params string[] protocols)
    {
        protocols = Validate.NotNull(nameof(protocols), protocols);

        if (protocols.Select(p => Validate.Rfc2616Token(nameof(protocols), p)).Any(_protocols.Contains))
        {
            throw new ArgumentException("Duplicate WebSocket protocol name.", nameof(protocols));
        }

        EnsureConfigurationNotLocked();

        _protocols.AddRange(protocols);
    }

    /// <summary>
    /// Sends a text payload.
    /// </summary>
    /// <param name="context">The web socket.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    private async Task SendAsync(IWebSocketContext context, string payload)
    {
        try
        {
            byte[] buffer = _encoding.GetBytes(payload);

            await context.WebSocket.SendAsync(buffer, true, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ex.Log(nameof(WebSocketModule));
        }
    }

    /// <summary>
    /// Sends a binary payload.
    /// </summary>
    /// <param name="context">The web socket.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    private async Task SendAsync(IWebSocketContext context, byte[] payload)
    {
        try
        {
            await context.WebSocket.SendAsync(payload, false, context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ex.Log(nameof(WebSocketModule));
        }
    }

    /// <summary>
    /// Broadcasts the specified payload to all connected WebSocket clients.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected Task BroadcastAsync(byte[] payload)
        => Task.WhenAll(_contexts.Values.Select(c => SendAsync(c, payload)));

    /// <summary>
    /// Broadcasts the specified payload to selected WebSocket clients.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="selector">A callback function that must return <see langword="true"/>
    /// for each context to be included in the broadcast.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected Task BroadcastAsync(byte[] payload, Func<IWebSocketContext, bool> selector)
        => Task.WhenAll(_contexts.Values.Where(Validate.NotNull(nameof(selector), selector)).Select(c => SendAsync(c, payload)));

    /// <summary>
    /// Broadcasts the specified payload to all connected WebSocket clients.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected Task BroadcastAsync(string payload)
        => Task.WhenAll(_contexts.Values.Select(c => SendAsync(c, payload)));

    /// <summary>
    /// Broadcasts the specified payload to selected WebSocket clients.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="selector">A callback function that must return <see langword="true"/>
    /// for each context to be included in the broadcast.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected Task BroadcastAsync(string payload, Func<IWebSocketContext, bool> selector)
        => Task.WhenAll(_contexts.Values.Where(Validate.NotNull(nameof(selector), selector)).Select(c => SendAsync(c, payload)));

    /// <summary>
    /// Closes the specified web socket, removes it and disposes it.
    /// </summary>
    /// <param name="context">The web socket.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    private async Task CloseAsync(IWebSocketContext? context)
    {
        if (context == null)
        {
            return;
        }

        try
        {
            await context.WebSocket.CloseAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ex.Log(nameof(WebSocketModule));
        }
        finally
        {
            RemoveWebSocket(context);
        }
    }

    /// <summary>
    /// Called when this WebSocket server receives a full message (EndOfMessage) from a client.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="result">The result.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected abstract Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result);

    /// <summary>
    /// Called when this WebSocket server receives a message frame regardless if the frame represents the EndOfMessage.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="result">The result.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected virtual Task OnFrameReceivedAsync(
        IWebSocketContext context,
        byte[] buffer,
        IWebSocketReceiveResult result)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when this WebSocket server accepts a new client.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected virtual Task OnClientConnectedAsync(IWebSocketContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Called when the server has removed a connected client for any reason.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected virtual Task OnClientDisconnectedAsync(IWebSocketContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposing)
        {
            return;
        }

        _isDisposing = true;

        if (disposing)
        {
            _connectionWatchdog?.Dispose();
            Task.WhenAll(_contexts.Values.Select(CloseAsync)).Await(false);
            PurgeDisconnectedContexts();
        }
    }

    private void RemoveWebSocket(IWebSocketContext context)
    {
        _ = _contexts.TryRemove(context.Id, out _);
        context.WebSocket.Dispose();

        // OnClientDisconnectedAsync is better called in its own task,
        // so it may call methods that require a lock on _contextsAccess.
        // Otherwise, calling e.g. Broadcast would result in a deadlock.
        _ = Task.Run(async () =>
        {
            try
            {
                await OnClientDisconnectedAsync(context).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                $"[{context.Id}] OnClientDisconnectedAsync was canceled.".Debug(nameof(WebSocketModule));
            }
            catch (Exception e)
            {
                e.Log(nameof(WebSocketModule), $"[{context.Id}] Exception in OnClientDisconnectedAsync.");
            }
        });
    }

    private void PurgeDisconnectedContexts()
    {
        ICollection<IWebSocketContext> contexts = _contexts.Values;
        int totalCount = _contexts.Count;
        int purgedCount = 0;
        foreach (IWebSocketContext context in contexts)
        {
            if (context.WebSocket.State == WebSocketState.Open)
            {
                continue;
            }

            RemoveWebSocket(context);
            purgedCount++;
        }

        $"{BaseRoute} - Purged {purgedCount} of {totalCount} sockets."
            .Debug(nameof(WebSocketModule));
    }

    private async Task ProcessNotioContext(IWebSocketContext context, CancellationToken cancellationToken)
    {
        ((Internal.WebSocket)context.WebSocket).OnMessage += async (_, e) =>
        {
            if (e.Opcode == Opcode.Close)
            {
                await context.WebSocket.CloseAsync(context.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                await OnMessageReceivedAsync(
                        context,
                        e.RawData,
                        new Internal.WebSocketReceiveResult(e.RawData.Length, e.Opcode))
                    .ConfigureAwait(false);
            }
        };

        while (context.WebSocket.State is WebSocketState.Open
            or WebSocketState.CloseReceived
            or WebSocketState.CloseSent)
        {
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessSystemContext(IWebSocketContext context, System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken)
    {
        // define a receive buffer
        byte[] receiveBuffer = new byte[ReceiveBufferSize];

        // define a dynamic buffer that holds multi-part receptions
        List<byte> receivedMessage = new(receiveBuffer.Length * 2);

        // poll the WebSocket connections for reception
        while (webSocket.State == WebSocketState.Open)
        {
            // retrieve the result (blocking)
            SystemWebSocketReceiveResult receiveResult = new(
                await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken)
                    .ConfigureAwait(false));

            if (receiveResult.MessageType == (int)WebSocketMessageType.Close)
            {
                // close the connection if requested by the client
                await webSocket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            byte[] frameBytes = new byte[receiveResult.Count];
            Array.Copy(receiveBuffer, frameBytes, frameBytes.Length);
            await OnFrameReceivedAsync(context, frameBytes, receiveResult).ConfigureAwait(false);

            // add the response to the multi-part response
            receivedMessage.AddRange(frameBytes);

            if (_maxMessageSize > 0 && receivedMessage.Count > _maxMessageSize)
            {
                // close the connection if message exceeds max length
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.MessageTooBig,
                    $"Message too big. Maximum is {_maxMessageSize} bytes.",
                    cancellationToken).ConfigureAwait(false);

                // exit the loop; we're done
                return;
            }

            // if we're at the end of the message, process the message
            if (!receiveResult.EndOfMessage)
            {
                continue;
            }

            await OnMessageReceivedAsync(context, [.. receivedMessage], receiveResult)
                .ConfigureAwait(false);
            receivedMessage.Clear();
        }
    }
}
