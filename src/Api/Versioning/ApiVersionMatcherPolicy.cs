using Microsoft.AspNetCore.Routing.Matching;

namespace Defra.WasteOrganisations.Api.Versioning;

/// <summary>
/// Routes a request to the endpoint that serves the API version it asks for, so one path can have a
/// handler per version. The policy splits the routing tree by version before the HTTP method is
/// matched, which keeps the 405 for a method v1 does not have. It only applies where an endpoint
/// declares versions with <see cref="EndpointConventionBuilderExtensions.WithApiVersions"/>, so
/// nothing changes until the first endpoint does.
/// <para>
/// A v1 request sees only the endpoints that support v1, so the v1 API stays exactly as it is. Any
/// other version sees the endpoints that support it, plus the v1 endpoints for the methods it does
/// not cover, so an unsupported version still reaches an endpoint and
/// <see cref="ApiVersioningMiddleware"/> returns 406 after authorisation. A version no endpoint
/// declares, or a malformed one, takes that same route.
/// </para>
/// </summary>
public sealed class ApiVersionMatcherPolicy : MatcherPolicy, INodeBuilderPolicy
{
    // Below HttpMethodMatcherPolicy (-1000), so the tree splits by version first and the method
    // check, with its 405, happens inside the requested version.
    public override int Order => -1100;

    // Edge for a version no endpoint at the node declares, or a malformed one.
    private const int OtherVersions = 0;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
        endpoints.Any(e => e.Metadata.GetMetadata<ApiVersionsMetadata>() is not null);

    public IReadOnlyList<PolicyNodeEdge> GetEdges(IReadOnlyList<Endpoint> endpoints)
    {
        var edges = endpoints
            .SelectMany(ApiVersionsMetadata.For)
            .Append(AcceptHeaderVersionResolver.DefaultVersion)
            .Distinct()
            .Order()
            .Select(version => new PolicyNodeEdge(version, EndpointsFor(version, endpoints)))
            .ToList();

        edges.Add(
            new PolicyNodeEdge(OtherVersions, WithFallbacks(AcceptHeaderVersionResolver.DefaultVersion, endpoints))
        );

        return edges;
    }

    public PolicyJumpTable BuildJumpTable(int exitDestination, IReadOnlyList<PolicyJumpTableEdge> edges)
    {
        var destinations = edges.ToDictionary(e => (int)e.State, e => e.Destination);

        return new ApiVersionJumpTable(destinations, destinations[OtherVersions]);
    }

    private static List<Endpoint> EndpointsFor(int version, IReadOnlyList<Endpoint> endpoints) =>
        version == AcceptHeaderVersionResolver.DefaultVersion
            // v1 is strict: a v1 client must not see a v2-only endpoint, so it keeps its 404 or 405.
            ? endpoints.Where(e => Supports(e, version)).ToList()
            : WithFallbacks(version, endpoints);

    /// <summary>
    /// The endpoints that support the version, plus those that do not but answer an HTTP method none
    /// of the supporting ones on the same route does. The middleware turns a request to a fallback
    /// into a 406. Fallbacks are decided route by route, so a v2 <c>/x/{id}</c> never stands in for a
    /// v1-only <c>/x/search</c>: the literal keeps its precedence and the request gets 406, as for any
    /// v1-only route. The input order is kept.
    /// </summary>
    private static List<Endpoint> WithFallbacks(int version, IReadOnlyList<Endpoint> endpoints)
    {
        var fallbacks = endpoints
            .GroupBy(Route)
            .SelectMany(route =>
                route.Where(e =>
                    !Supports(e, version) && !route.Any(s => Supports(s, version) && SharesHttpMethod(s, e))
                )
            )
            .ToHashSet();

        return endpoints.Where(e => Supports(e, version) || fallbacks.Contains(e)).ToList();
    }

    private static bool Supports(Endpoint endpoint, int version) => ApiVersionsMetadata.For(endpoint).Contains(version);

    private static string? Route(Endpoint endpoint) => (endpoint as RouteEndpoint)?.RoutePattern.RawText;

    private static bool SharesHttpMethod(Endpoint first, Endpoint second)
    {
        var firstMethods = HttpMethods(first);
        var secondMethods = HttpMethods(second);

        // An endpoint without method metadata answers every method.
        return firstMethods is null
            || secondMethods is null
            || firstMethods.Intersect(secondMethods, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static IReadOnlyList<string>? HttpMethods(Endpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;

        return methods is { Count: > 0 } ? methods : null;
    }

    private sealed class ApiVersionJumpTable(IReadOnlyDictionary<int, int> destinations, int otherDestination)
        : PolicyJumpTable
    {
        public override int GetDestination(HttpContext httpContext) =>
            AcceptHeaderVersionResolver.TryResolve(httpContext.Request.Headers.Accept.ToArray(), out var version)
            && destinations.TryGetValue(version, out var destination)
                ? destination
                : otherDestination;
    }
}
