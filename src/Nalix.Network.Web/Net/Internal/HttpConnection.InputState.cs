namespace Nalix.Network.Web.Net.Internal;

internal partial class HttpConnection
{
    private enum InputState
    {
        RequestLine,
        Headers,
    }
}