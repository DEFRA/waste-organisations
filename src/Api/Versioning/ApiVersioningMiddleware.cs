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
        if (IsExemptPath(context.Request.Path))
        {
            await next(context);

            return;
        }

        // Only route endpoints (those mapped with MapGet and friends) carry a version. No endpoint means
        // routing's own 404; routing's 405 for a method no endpoint answers is a plain Endpoint, not a
        // RouteEndpoint, and stays a 405 whatever the version asks for.
        if (context.GetEndpoint() is not RouteEndpoint endpoint)
        {
            await next(context);

            return;
        }

        if (!AcceptHeaderVersionResolver.TryResolve(context.Request.Headers.Accept.ToArray(), out var requestedVersion))
        {
            await WriteUnsupportedVersionProblem(context);

            return;
        }

        var supportedVersions =
            endpoint.Metadata.GetMetadata<ApiVersionsMetadata>()?.Versions ?? ApiVersionsMetadata.Default;

        if (!supportedVersions.Contains(requestedVersion))
        {
            await WriteUnsupportedVersionProblem(context);

            return;
        }

        await next(context);
    }

    private static bool IsExemptPath(PathString path) =>
        ExemptPathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static Task WriteUnsupportedVersionProblem(HttpContext context) =>
        TypedResults
            .Problem(statusCode: StatusCodes.Status406NotAcceptable, detail: "Unsupported API version")
            .ExecuteAsync(context);
}
