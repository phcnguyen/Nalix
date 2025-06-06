using Nalix.Network.Web.Http;
using System.Threading.Tasks;

namespace Nalix.Network.Web.Routing;

/// <summary>
/// BaseValue36 class for callbacks used to handle routed requests.
/// </summary>
/// <param name="context">An <see cref="IHttpContext" /> interface representing the context of the request.</param>
/// <param name="route">The matched route.</param>
/// <returns>A <see cref="Task" /> representing the ongoing operation.</returns>
/// <seealso cref="RouteMatch"/>
public delegate Task RouteHandlerCallback(IHttpContext context, RouteMatch route);