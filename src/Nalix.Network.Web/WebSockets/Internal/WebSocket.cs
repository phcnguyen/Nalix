using Nalix.Extensions;
using Nalix.Network.Web.Enums;
using Nalix.Network.Web.Http;
using Nalix.Network.Web.Net.Internal;
using Nalix.Network.Web.WebSockets.Internal.Enums;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Web.WebSockets.Internal;

/// <summary>
/// Implements the WebSocket interface.
/// </summary>
/// <remarks>
/// The WebSocket class provides a set of methods and properties for two-way communication using
/// the WebSocket protocol (<see href="http://tools.ietf.org/html/rfc6455">RFC 6455</see>).
/// </remarks>
internal sealed class WebSocket : IWebSocket
{
    public const string SupportedVersion = "13";

    private readonly Lock _stateSyncRoot = new();
    private readonly ConcurrentQueue<MessageEventArgs> _messageEventQueue = new();
    private readonly Action _closeConnection;
    private readonly TimeSpan _waitTime = TimeSpan.FromSeconds(1);

    private volatile WebSocketState _readyState;
    private AutoResetEvent? _exitReceiving;
    private FragmentBuffer? _fragmentsBuffer;
    private volatile bool _inMessage;
    private AutoResetEvent? _receivePong;
    private Stream? _stream;

    private WebSocket(HttpConnection connection, bool emitOnPing)
    {
        EmitOnPing = emitOnPing;
        _closeConnection = connection.ForceClose;
        _stream = connection.Stream;
        _readyState = WebSocketState.Open;
    }

    ~WebSocket() => Dispose(false);

    /// <summary>
    /// Occurs when the <see cref="WebSocket"/> receives a message.
    /// </summary>
    public event EventHandler<MessageEventArgs>? OnMessage;

    /// <inheritdoc />
    public WebSocketState State => _readyState;

    internal static CompressionMethod Compression => CompressionMethod.None;

    internal bool EmitOnPing { get; set; }

    internal bool InContinuation { get; private set; }

    /// <inheritdoc />
    public Task SendAsync(byte[] buffer, bool isText, CancellationToken cancellationToken)
        => SendAsync(buffer, isText ? Opcode.Text : Opcode.Binary, cancellationToken);

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken = default)
        => CloseAsync(CloseStatusCode.Normal, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task CloseAsync(
        CloseStatusCode code = CloseStatusCode.Undefined,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        bool CheckParametersForClose()
        {
            switch (code)
            {
                case CloseStatusCode.NoStatus when !string.IsNullOrEmpty(reason):
                    Trace.WriteLine("'code' cannot have a reason.", nameof(WebSocket));
                    return false;

                case CloseStatusCode.MandatoryExtension:
                    Trace.WriteLine("'code' cannot be used by a server.", nameof(WebSocket));
                    return false;
            }

            if (string.IsNullOrEmpty(reason) || Encoding.UTF8.GetBytes(reason).Length <= 123) return true;
            Trace.WriteLine("'reason' is greater than the allowable max size.", nameof(WebSocket));
            return false;
        }

        if (_readyState != WebSocketState.Open)
        {
            return Task.CompletedTask;
        }

        if (code != CloseStatusCode.Undefined && !CheckParametersForClose())
        {
            return Task.CompletedTask;
        }

        if (code == CloseStatusCode.NoStatus)
        {
            return InternalCloseAsync(cancellationToken: cancellationToken);
        }

        bool send = !IsOpcodeReserved(code);
        return InternalCloseAsync(new PayloadData((ushort)code, reason), send, send, cancellationToken);
    }

    /// <summary>
    /// Sends a ping using the WebSocket connection.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocket"/> receives a pong to this ping in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    public Task<bool> PingAsync()
        => PingAsync(WebSocketFrame.EmptyPingBytes, _waitTime);

    /// <summary>
    /// Sends a ping with the specified <paramref name="message"/> using the WebSocket connection.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocket"/> receives a pong to this ping in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that represents the message to send.
    /// </param>
    public Task<bool> PingAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return PingAsync();
        }

        byte[] data = Encoding.UTF8.GetBytes(message);

        if (data.Length <= 125)
        {
            return PingAsync(WebSocketFrame.CreatePingFrame(data).ToArray(), _waitTime);
        }

        Debug.WriteLine("A message has greater than the allowable max size.", nameof(PingAsync));

        return Task.FromResult(false);
    }

    /// <summary>
    /// Sends binary <paramref name="data" /> using the WebSocket connection.
    /// </summary>
    /// <param name="data">An array of <see cref="byte" /> that represents the binary data to send.</param>
    /// <param name="opcode">The opcode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A task that represents the asynchronous of send
    /// binary data using websocket.
    /// </returns>

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public async Task SendAsync(byte[] data, Opcode opcode, CancellationToken cancellationToken = default)
    {
        if (_readyState != WebSocketState.Open)
        {
            throw new WebSocketException(CloseStatusCode.Normal, $"This operation isn\'t available in: {_readyState}");
        }

        await using WebSocketStream stream = new(data, opcode, Compression);
        foreach (WebSocketFrame frame in stream.GetFrames())
        {
            await Send(frame).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal static async Task<WebSocket> AcceptAsync(HttpListenerContext httpContext, string acceptedProtocol)
    {
        static string CreateResponseKey(string clientKey)
        {
            const string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

            StringBuilder buff = new StringBuilder(clientKey, 64).Append(guid);
            return Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(buff.ToString())));
        }

        System.Collections.Specialized.NameValueCollection requestHeaders = httpContext.Request.Headers;

        string? webSocketKey = requestHeaders[HttpHeaderNames.SecWebSocketKey];

        if (string.IsNullOrEmpty(webSocketKey))
        {
            throw new WebSocketException(CloseStatusCode.ProtocolError,
                $"Includes no {HttpHeaderNames.SecWebSocketKey} header, or it has an invalid value.");
        }

        string? webSocketVersion = requestHeaders[HttpHeaderNames.SecWebSocketVersion];

        if (webSocketVersion is null or not SupportedVersion)
        {
            throw new WebSocketException(CloseStatusCode.ProtocolError,
                $"Includes no {HttpHeaderNames.SecWebSocketVersion} header, or it has an invalid value.");
        }

        WebSocketHandshakeResponse handshakeResponse = new(httpContext)
        {
            Headers =
            {
                [HttpHeaderNames.SecWebSocketAccept] = CreateResponseKey(webSocketKey)
            }
        };

        if (acceptedProtocol.Length > 0)
        {
            handshakeResponse.Headers[HttpHeaderNames.SecWebSocketProtocol] = acceptedProtocol;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(handshakeResponse.ToString());
        await httpContext.Connection.Stream.WriteAsync(bytes).ConfigureAwait(false);

        // Signal the original response that headers have been sent.
        httpContext.HttpListenerResponse.HeadersSent = true;

        WebSocket socket = new(httpContext.Connection, false);
        socket.Open();
        return socket;
    }

    internal async Task<bool> PingAsync(byte[] frameAsBytes, TimeSpan timeout)
    {
        if (_readyState != WebSocketState.Open)
        {
            return false;
        }
        if (_stream != null)
        {
            await _stream.WriteAsync(frameAsBytes).ConfigureAwait(false);
        }

        return _receivePong != null && _receivePong.WaitOne(timeout);
    }

    private static bool IsOpcodeReserved(CloseStatusCode code)
    {
        return code is CloseStatusCode.Undefined
                or CloseStatusCode.NoStatus
                or CloseStatusCode.Abnormal
                or CloseStatusCode.TlsHandshakeFailure;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    private void Dispose(bool disposing)
    {
        try
        {
            InternalCloseAsync(new PayloadData((ushort)CloseStatusCode.Away)).Await();
        }
        catch
        {
            // Ignored
        }
    }

    private async Task InternalCloseAsync(
        PayloadData? payloadData = null,
        bool send = true,
        bool receive = true,
        CancellationToken cancellationToken = default)
    {
        lock (_stateSyncRoot)
        {
            if (_readyState is WebSocketState.CloseReceived or WebSocketState.CloseSent)
            {
                Trace.WriteLine("The closing is already in progress.", nameof(InternalCloseAsync));
                return;
            }

            if (_readyState == WebSocketState.Closed)
            {
                Trace.WriteLine("The connection has been closed.", nameof(InternalCloseAsync));
                return;
            }

            send = send && _readyState == WebSocketState.Open;
            receive = receive && send;

            _readyState = WebSocketState.CloseSent;
        }

        Trace.WriteLine("Begin closing the connection.", nameof(InternalCloseAsync));

        byte[]? bytes = send ? WebSocketFrame.CreateCloseFrame(payloadData).ToArray() : null;
        await CloseHandshakeAsync(bytes, receive, cancellationToken).ConfigureAwait(false);
        ReleaseResources();

        Trace.WriteLine("End closing the connection.", nameof(InternalCloseAsync));

        lock (_stateSyncRoot)
        {
            _readyState = WebSocketState.Closed;
        }
    }

    private async Task CloseHandshakeAsync(
        byte[]? frameAsBytes,
        bool receive,
        CancellationToken cancellationToken)
    {
        bool sent = frameAsBytes != null;

        if (sent && _stream != null)
        {
            await _stream.WriteAsync(frameAsBytes, cancellationToken).ConfigureAwait(false);
        }

        if (receive && sent)
        {
            _ = _exitReceiving?.WaitOne(_waitTime);
        }
    }

    private void Fatal(string message, Exception? exception = null)
    {
        Fatal(message, (exception as WebSocketException)?.Code ?? CloseStatusCode.Abnormal);
    }

    private void Fatal(string message, CloseStatusCode code)
    {
        InternalCloseAsync(new PayloadData((ushort)code, message), !IsOpcodeReserved(code), false).Await();
    }

    private void Message()
    {
        if (_inMessage || _messageEventQueue.IsEmpty || _readyState != WebSocketState.Open)
        {
            return;
        }

        _inMessage = true;

        if (_messageEventQueue.TryDequeue(out MessageEventArgs? e))
        {
            Messages(e);
        }
    }

    private void Messages(MessageEventArgs? e)
    {
        try
        {
            if (e is not null)
            {
                OnMessage?.Invoke(this, e);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"An exception has occurred while invoking the OnMessage event: {ex}", nameof(WebSocket));
        }

        if (!_messageEventQueue.TryDequeue(out e) || _readyState != WebSocketState.Open)
        {
            _inMessage = false;
            return;
        }

        _ = Task.Run(() => Messages(e));
    }

    private void Open()
    {
        _inMessage = true;
        StartReceiving();

        if (!_messageEventQueue.TryDequeue(out MessageEventArgs? e) || _readyState != WebSocketState.Open)
        {
            _inMessage = false;
            return;
        }

        Messages(e);
    }

    private Task ProcessCloseFrame(WebSocketFrame frame)
    {
        return InternalCloseAsync(frame.PayloadData, !frame.PayloadData.HasReservedCode, false);
    }

    private async Task ProcessDataFrame(WebSocketFrame frame)
    {
        if (frame.IsCompressed)
        {
            using MemoryStream ms = await frame.PayloadData.ApplicationData.CompressAsync(Compression, false, CancellationToken.None).ConfigureAwait(false);

            _messageEventQueue.Enqueue(new MessageEventArgs(frame.Opcode, ms.ToArray()));
        }
        else
        {
            _messageEventQueue.Enqueue(new MessageEventArgs(frame));
        }
    }

    private async Task ProcessFragmentFrame(WebSocketFrame frame)
    {
        if (!InContinuation)
        {
            // Must process first fragment.
            if (frame.Opcode == Opcode.Cont)
            {
                return;
            }

            _fragmentsBuffer = new FragmentBuffer(frame.Opcode, frame.IsCompressed);
            InContinuation = true;
        }

        _fragmentsBuffer?.AddPayload(frame.PayloadData.ApplicationData);

        if (frame.Fin == Fin.Final)
        {
            await using (_fragmentsBuffer)
            {
                if (_fragmentsBuffer != null)
                {
                    _messageEventQueue.Enqueue(await _fragmentsBuffer.GetMessage(Compression).ConfigureAwait(false));
                }
            }

            _fragmentsBuffer = null;
            InContinuation = false;
        }
    }

    private Task ProcessPingFrame(WebSocketFrame frame)
    {
        if (EmitOnPing)
        {
            _messageEventQueue.Enqueue(new MessageEventArgs(frame));
        }

        return Send(new WebSocketFrame(Opcode.Pong, frame.PayloadData));
    }

    private void ProcessPongFrame()
    {
        _ = _receivePong?.Set();
        Trace.WriteLine("Received a pong.", nameof(ProcessPongFrame));
    }

    private async Task<bool> ProcessReceivedFrame(WebSocketFrame frame)
    {
        if (frame.IsFragment)
        {
            await ProcessFragmentFrame(frame).ConfigureAwait(false);
        }
        else
        {
            switch (frame.Opcode)
            {
                case Opcode.Text:
                case Opcode.Binary:
                    await ProcessDataFrame(frame).ConfigureAwait(false);
                    break;

                case Opcode.Ping:
                    await ProcessPingFrame(frame).ConfigureAwait(false);
                    break;

                case Opcode.Pong:
                    ProcessPongFrame();
                    break;

                case Opcode.Close:
                    await ProcessCloseFrame(frame).ConfigureAwait(false);
                    break;

                default:
                    Fatal($"Unsupported frame received: {frame.PrintToString()}", CloseStatusCode.PolicyViolation);
                    return false;
            }
        }

        return true;
    }

    private void ReleaseResources()
    {
        _closeConnection();
        _stream = null;

        if (_fragmentsBuffer != null)
        {
            _fragmentsBuffer.Dispose();
            _fragmentsBuffer = null;
            InContinuation = false;
        }

        if (_receivePong != null)
        {
            _receivePong.Dispose();
            _receivePong = null;
        }

        if (_exitReceiving == null)
        {
            return;
        }

        _exitReceiving.Dispose();
        _exitReceiving = null;
    }

    private Task Send(WebSocketFrame frame)
    {
        lock (_stateSyncRoot)
        {
            if (_readyState != WebSocketState.Open)
            {
                Debug.WriteLine("The sending has been interrupted.", nameof(Send));
                return Task.Delay(0);
            }
        }

        byte[] frameAsBytes = frame.ToArray();

        return _stream is not null
            ? _stream.WriteAsync(frameAsBytes, 0, frameAsBytes.Length)
            : throw new InvalidOperationException("Stream is null.");
    }

    private void StartReceiving()
    {
        while (_messageEventQueue.TryDequeue(out _))
        {
            // do nothing
        }

        _exitReceiving = new AutoResetEvent(false);
        _receivePong = new AutoResetEvent(false);

        WebSocketFrameStream frameStream = new(_stream);

        _ = Task.Run(async () =>
        {
            while (_readyState == WebSocketState.Open)
            {
                try
                {
                    WebSocketFrame? frame = await frameStream.ReadFrameAsync(this).ConfigureAwait(false);

                    if (frame == null)
                    {
                        return;
                    }

                    bool result = await ProcessReceivedFrame(frame).ConfigureAwait(false);

                    if (!result || _readyState == WebSocketState.Closed)
                    {
                        _ = _exitReceiving?.Set();

                        return;
                    }

                    _ = Task.Run(Message);
                }
                catch (Exception ex)
                {
                    Fatal("An exception has occurred while receiving.", ex);
                }
            }
        });
    }
}
