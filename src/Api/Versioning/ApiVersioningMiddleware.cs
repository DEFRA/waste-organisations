namespace Defra.WasteOrganisations.Api.Versioning;

/// <summary>
/// Checks the requested API version against the matched endpoint's declared versions, after
/// authentication and authorisation and before the endpoint handler - see specs/0004-REGEN-167.
/// Handlers, services and the data layer never see the version.
/// </summary>
public class ApiVersioningMiddleware : IMiddleware
{
    internal static readonly string[] ExemptPathPrefixes = ["/health", "/openapi", "/redoc"];

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Exempt paths are not versioned. Nor is anything but a route endpoint (what MapGet and friends
        // create): no endpoint is routing's own 404, and routing's 405 for a method no endpoint answers
        // is a plain Endpoint, so both pass through whatever the version asks for.
        if (IsExemptPath(context.Request.Path) || context.GetEndpoint() is not RouteEndpoint endpoint)
        {
            await next(context);

            return;
        }

        var isSupported =
            AcceptHeaderVersionResolver.TryResolve(context.Request.Headers.Accept.ToArray(), out var requestedVersion)
            && ApiVersionsMetadata.For(endpoint).Contains(requestedVersion);

        if (isSupported)
            await next(context);
        else
            await WriteUnsupportedVersionProblem(context);
    }

    private static bool IsExemptPath(PathString path) =>
        ExemptPathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static Task WriteUnsupportedVersionProblem(HttpContext context) =>
        TypedResults
            .Problem(statusCode: StatusCodes.Status406NotAcceptable, detail: "Unsupported API version")
            .ExecuteAsync(context);
}
