using Nalix.Network.Web.Enums;
using System;

namespace Nalix.Network.Web.Routing;

public static partial class RoutingModuleExtensions
{
    /// <summary>
    /// Adds a handler to a <see cref="RoutingModule"/>.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="verb">A <see cref="HttpVerbs"/> constant representing the HTTP method
    /// to associate with <paramref name="handler"/>, or <see cref="HttpVerbs.Any"/>
    /// if <paramref name="handler"/> can handle all HTTP methods.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    /// <seealso cref="RoutingModule.Add(HttpVerbs,RouteMatcher,RouteHandlerCallback)"/>
    public static RoutingModule Handle(this RoutingModule @this, HttpVerbs verb, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(verb, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Adds a synchronous handler to a <see cref="RoutingModule"/>.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="verb">A <see cref="HttpVerbs"/> constant representing the HTTP method
    /// to associate with <paramref name="handler"/>, or <see cref="HttpVerbs.Any"/>
    /// if <paramref name="handler"/> can handle all HTTP methods.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    /// <seealso cref="RoutingModule.Add(HttpVerbs,RouteMatcher,RouteHandlerCallback)"/>
    public static RoutingModule Handle(this RoutingModule @this, HttpVerbs verb, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(verb, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates all requests matching a route to a handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnAny(this RoutingModule @this, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Any, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates all requests matching a route to a synchronous handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnAny(this RoutingModule @this, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Any, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>DELETE</c> requests matching a route to a handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnDelete(this RoutingModule @this, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Delete, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>DELETE</c> requests matching a route to a synchronous handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnDelete(this RoutingModule @this, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Delete, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>GET</c> requests matching a route to a handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnGet(this RoutingModule @this, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Get, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>GET</c> requests matching a route to a synchronous handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnGet(this RoutingModule @this, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Get, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>HEAD</c> requests matching a route to a handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnHead(this RoutingModule @this, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Head, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>HEAD</c> requests matching a route to a synchronous handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnHead(this RoutingModule @this, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Head, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>OPTIONS</c> requests matching a route to a handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnOptions(this RoutingModule @this, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Options, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>OPTIONS</c> requests matching a route to a synchronous handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnOptions(this RoutingModule @this, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Options, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>PATCH</c> requests matching a route to a handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnPatch(this RoutingModule @this, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Patch, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>PATCH</c> requests matching a route to a synchronous handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnPatch(this RoutingModule @this, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Patch, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>POST</c> requests matching a route to a handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnPost(this RoutingModule @this, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Post, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>POST</c> requests matching a route to a synchronous handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnPost(this RoutingModule @this, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Post, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>PUT</c> requests matching a route to a handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnPut(this RoutingModule @this, string route, bool isBaseRoute, RouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Put, matcher, handler);
        return @this;
    }

    /// <summary>
    /// Associates <c>PUT</c> requests matching a route to a synchronous handler.
    /// </summary>
    /// <param name="this">The <see cref="RoutingModule"/> on which this method is called.</param>
    /// <param name="route">The route to match URL paths against.</param>
    /// <param name="isBaseRoute"><see langword="true"/> if <paramref name="route"/>
    /// is a base route; <see langword="false"/> if <paramref name="route"/>
    /// is a terminal (non-base) route.</param>
    /// <param name="handler">A callback used to handle matching contexts.</param>
    /// <returns><paramref name="this"/> with the handler added.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">
    /// <para><paramref name="route"/> is <see langword="null"/>.</para>
    /// <para>- or -</para>
    /// <para><paramref name="handler"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="FormatException"><paramref name="route"/> is not a valid route.</exception>
    public static RoutingModule OnPut(this RoutingModule @this, string route, bool isBaseRoute, SyncRouteHandlerCallback handler)
    {
        var matcher = RouteMatcher.Parse(route, isBaseRoute) ?? throw new FormatException("Invalid route format.");
        @this.Add(HttpVerbs.Put, matcher, handler);
        return @this;
    }
}