using Nalix.Network.Web.Http;

namespace Nalix.Network.Web.Routing;

/// <summary>
/// BaseValue36 class for callbacks used to handle routed requests synchronously.
/// </summary>
/// <param name="context">An <see cref="IHttpContext" /> interface representing the context of the request.</param>
/// <param name="route">The matched route.</param>
/// <seealso cref="RouteMatch"/>
public delegate void SyncRouteHandlerCallback(IHttpContext context, RouteMatch route);