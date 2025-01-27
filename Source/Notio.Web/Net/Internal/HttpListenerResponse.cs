﻿using Notio.Utilities;
using Notio.Web.Http;
using Notio.Web.Utilities;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Notio.Web.Net.Internal;

/// <summary>
/// Represents an HTTP Listener's response.
/// </summary>
/// <seealso cref="IDisposable" />
internal sealed class HttpListenerResponse : IHttpResponse, IDisposable
{
    private readonly HttpConnection _connection;
    private readonly HttpListenerRequest _request;
    private readonly string _id;
    private bool _disposed;
    private string _contentType = MimeType.Html; // Same default value as Microsoft's implementation
    private CookieList? _cookies;
    private bool _keepAlive;
    private ResponseStream? _outputStream;
    private int _statusCode = 200;
    private bool _chunked;

    internal HttpListenerResponse(HttpListenerContext context)
    {
        _request = context.HttpListenerRequest;
        _connection = context.Connection;
        _id = context.Id;
        _keepAlive = context.Request.KeepAlive;
    }

    /// <inheritdoc />
    public Encoding? ContentEncoding { get; set; } = WebServer.DefaultEncoding;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">This property is being set and headers were already sent.</exception>
    public long ContentLength64
    {
        get => Headers.ContainsKey(HttpHeaderNames.ContentLength) && long.TryParse(Headers[HttpHeaderNames.ContentLength], out var val) ? val : 0;

        set
        {
            EnsureCanChangeHeaders();
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Must be >= 0");
            }

            Headers[HttpHeaderNames.ContentLength] = value.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">This property is being set and headers were already sent.</exception>
    /// <exception cref="ArgumentNullException">This property is being set to <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">This property is being set to the empty string.</exception>
    public string ContentType
    {
        get => _contentType;

        set
        {
            EnsureCanChangeHeaders();
            _contentType = Validate.NotNullOrEmpty(nameof(value), value);
        }
    }

    /// <inheritdoc />
    public ICookieCollection Cookies => CookieCollection;

    /// <inheritdoc />
    public WebHeaderCollection Headers { get; } = [];

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">This property is being set and headers were already sent.</exception>
    public bool KeepAlive
    {
        get => _keepAlive;

        set
        {
            EnsureCanChangeHeaders();
            _keepAlive = value;
        }
    }

    /// <inheritdoc />
    public Stream OutputStream => _outputStream ??= _connection.GetResponseStream();

    /// <inheritdoc />
    public Version ProtocolVersion => _request.ProtocolVersion;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">This property is being set and headers were already sent.</exception>
    public bool SendChunked
    {
        get => _chunked;

        set
        {
            EnsureCanChangeHeaders();
            _chunked = value;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">This property is being set and headers were already sent.</exception>
    public int StatusCode
    {
        get => _statusCode;

        set
        {
            EnsureCanChangeHeaders();
            if (value < 100 || value > 999)
            {
                throw new ArgumentOutOfRangeException(nameof(StatusCode), "StatusCode must be between 100 and 999.");
            }

            _statusCode = value;
            StatusDescription = HttpListenerResponseHelper.GetStatusDescription(value);
        }
    }

    /// <inheritdoc />
    public string StatusDescription { get; set; } = "OK";

    internal CookieList CookieCollection
    {
        get => _cookies ??= [];
        set => _cookies = value;
    }

    internal bool HeadersSent { get; set; }

    void IDisposable.Dispose() => Close(true);

    public void Close()
    {
        if (!_disposed)
        {
            Close(false);
        }
    }

    /// <inheritdoc />
    public void SetCookie(Cookie cookie)
    {
        ArgumentNullException.ThrowIfNull(cookie);

        if (_cookies != null)
        {
            if (_cookies.Any(c => cookie.Name == c.Name && cookie.Domain == c.Domain && cookie.Path == c.Path))
            {
                throw new ArgumentException("The cookie already exists.");
            }
        }
        else
        {
            _cookies = [];
        }

        _cookies.Add(cookie);
    }

    internal MemoryStream SendHeaders(bool closing)
    {
        if (_contentType != null)
        {
            var contentTypeValue = !_contentType.Contains("charset=")
                ? $"{_contentType}; charset={WebServer.DefaultEncoding.WebName}"
                : _contentType;

            Headers.Add(HttpHeaderNames.ContentType, contentTypeValue);
        }

        if (Headers[HttpHeaderNames.Server] == null)
        {
            Headers.Add(HttpHeaderNames.Server, WebServer.Signature);
        }

        if (Headers[HttpHeaderNames.Date] == null)
        {
            Headers.Add(HttpHeaderNames.Date, HttpDate.Format(DateTime.UtcNow));
        }

        if (closing)
        {
            Headers[HttpHeaderNames.ContentLength] = "0";
            _chunked = false;
        }
        else
        {
            if (ProtocolVersion < HttpVersion.Version11)
            {
                _chunked = false;
            }

            var haveContentLength = !_chunked
                                 && Headers.ContainsKey(HttpHeaderNames.ContentLength)
                                 && long.TryParse(Headers[HttpHeaderNames.ContentLength], out var contentLength)
                                 && contentLength >= 0L;

            if (!haveContentLength)
            {
                Headers.Remove(HttpHeaderNames.ContentLength);
                if (ProtocolVersion >= HttpVersion.Version11)
                {
                    _chunked = true;
                }
            }
        }

        if (_chunked)
        {
            Headers.Add(HttpHeaderNames.TransferEncoding, "chunked");
        }

        //// Apache forces closing the connection for these status codes:
        //// HttpStatusCode.BadRequest            400
        //// HttpStatusCode.RequestTimeout        408
        //// HttpStatusCode.LengthRequired        411
        //// HttpStatusCode.RequestEntityTooLarge 413
        //// HttpStatusCode.RequestUriTooLong     414
        //// HttpStatusCode.InternalServerError   500
        //// HttpStatusCode.ServiceUnavailable    503
        var reuses = _connection.Reuses;
        var keepAlive = _statusCode switch
        {
            400 => false,
            408 => false,
            411 => false,
            413 => false,
            414 => false,
            500 => false,
            503 => false,
            _ => _keepAlive && reuses < 100
        };

        _keepAlive = keepAlive;
        if (keepAlive)
        {
            Headers.Add(HttpHeaderNames.Connection, "keep-alive");
            if (ProtocolVersion >= HttpVersion.Version11)
            {
                Headers.Add(HttpHeaderNames.KeepAlive, $"timeout=15,max={100 - reuses}");
            }
        }
        else
        {
            Headers.Add(HttpHeaderNames.Connection, "close");
        }

        return WriteHeaders();
    }

    private static void AppendSetCookieHeader(StringBuilder sb, Cookie cookie)
    {
        if (cookie.Name.Length == 0)
        {
            return;
        }

        _ = sb.Append("Set-Cookie: ");

        if (cookie.Version > 0)
        {
            _ = sb.Append("Version=").Append(cookie.Version).Append("; ");
        }

        _ = sb
            .Append(cookie.Name)
            .Append('=')
            .Append(cookie.Value);

        if (cookie.Expires != DateTime.MinValue)
        {
            _ = sb
                .Append("; Expires=")
                .Append(HttpDate.Format(cookie.Expires));
        }

        if (!string.IsNullOrEmpty(cookie.Path))
        {
            _ = sb.Append("; Path=").Append(QuotedString(cookie, cookie.Path));
        }

        if (!string.IsNullOrEmpty(cookie.Domain))
        {
            _ = sb.Append("; Domain=").Append(QuotedString(cookie, cookie.Domain));
        }

        if (!string.IsNullOrEmpty(cookie.Port))
        {
            _ = sb.Append("; Port=").Append(cookie.Port);
        }

        if (cookie.Secure)
        {
            _ = sb.Append("; Secure");
        }

        if (cookie.HttpOnly)
        {
            _ = sb.Append("; HttpOnly");
        }

        _ = sb.Append("\r\n");
    }

    private static string QuotedString(Cookie cookie, string value)
        => cookie.Version == 0 || value.IsToken() ? value : "\"" + value.Replace("\"", "\\\"") + "\"";

    private void Close(bool force)
    {
        _disposed = true;

        _connection.Close(force);
    }

    private string GetHeaderData()
    {
        var sb = new StringBuilder()
            .Append("HTTP/")
            .Append(ProtocolVersion)
            .Append(' ')
            .Append(_statusCode)
            .Append(' ')
            .Append(StatusDescription)
            .Append("\r\n");

        foreach (var key in Headers.AllKeys.Where(x => x != "Set-Cookie"))
        {
            _ = sb
                .Append(key)
                .Append(": ")
                .Append(Headers[key])
                .Append("\r\n");
        }

        if (_cookies != null)
        {
            foreach (var cookie in _cookies)
            {
                AppendSetCookieHeader(sb, cookie);
            }
        }

        if (Headers.ContainsKey(HttpHeaderNames.SetCookie))
        {
            foreach (var cookie in CookieList.Parse(Headers[HttpHeaderNames.SetCookie] ?? string.Empty))
            {
                AppendSetCookieHeader(sb, cookie);
            }
        }

        return sb.Append("\r\n").ToString();
    }

    private MemoryStream WriteHeaders()
    {
        var stream = new MemoryStream();
        var data = WebServer.DefaultEncoding.GetBytes(GetHeaderData());
        var preamble = WebServer.DefaultEncoding.GetPreamble();
        stream.Write(preamble, 0, preamble.Length);
        stream.Write(data, 0, data.Length);

        _outputStream ??= _connection.GetResponseStream();

        // Assumes that the ms was at position 0
        stream.Position = preamble.Length;
        HeadersSent = true;

        return stream;
    }

    private void EnsureCanChangeHeaders()
    {
        ObjectDisposedException.ThrowIf(_disposed, _id);

        if (HeadersSent)
        {
            throw new InvalidOperationException("Header values cannot be changed after headers are sent.");
        }
    }
}