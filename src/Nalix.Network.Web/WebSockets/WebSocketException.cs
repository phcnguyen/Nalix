using Nalix.Network.Web.Enums;
using System;

namespace Nalix.Network.Web.WebSockets;

/// <summary>
/// The exception that is thrown when a WebSocket gets a fatal error.
/// </summary>

public class WebSocketException : Exception
{
    internal WebSocketException(string? message = null)
        : this(CloseStatusCode.Abnormal, message)
    {
        // Ignore
    }

    internal WebSocketException(CloseStatusCode code, Exception? innerException = null)
        : this(code, null, innerException)
    {
    }

    internal WebSocketException(CloseStatusCode code, string? message, Exception? innerException = null)
        : base(message ?? GetMessage(code), innerException)
    {
        Code = code;
    }

    /// <summary>
    /// Gets the status code indicating the cause of the exception.
    /// </summary>
    /// <value>
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
    /// indicating the cause of the exception.
    /// </value>
    public CloseStatusCode Code { get; }

    private static string GetMessage(CloseStatusCode code)
    {
        return code switch
        {
            CloseStatusCode.ProtocolError => "A WebSocket protocol error has occurred.",
            CloseStatusCode.UnsupportedData => "Unsupported data has been received.",
            CloseStatusCode.Abnormal => "An exception has occurred.",
            CloseStatusCode.InvalidData => "Invalid data has been received.",
            CloseStatusCode.PolicyViolation => "A policy violation has occurred.",
            CloseStatusCode.TooBig => "A too big message has been received.",
            CloseStatusCode.MandatoryExtension => "WebSocket client didn't receive expected extension(s).",
            CloseStatusCode.ServerError => "WebSocket server got an internal error.",
            CloseStatusCode.TlsHandshakeFailure => "An error has occurred during a TLS handshake.",
            _ => string.Empty
        };
    }
}
