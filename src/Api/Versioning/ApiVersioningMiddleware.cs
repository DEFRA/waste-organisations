namespace Defra.WasteOrganisations.Api.Versioning;

/// <summary>
/// Checks the requested API version against the matched endpoint's declared versions, after
/// authentication and authorisation and before the endpoint handler - see specs/0004-REGEN-167.
/// Handlers, services and the data layer never see the version.
/// </summary>
public class ApiVersioningMiddleware : IMiddleware
{
    internal static readonly string[] ExemptPathPrefixes = ["/health", "/openapi", "/redoc"];

    private static readonly IReadOnlySet<int> s_defaultVersions = new HashSet<int>
    {
        AcceptHeaderVersionResolver.DefaultVersion,
    };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (IsExemptPath(context.Request.Path))
        {
            await next(context);

            return;
        }

        var endpoint = context.GetEndpoint();

        if (endpoint is null)
        {
            // No endpoint matched: let routing produce its own 404.
            await next(context);

            return;
        }

        if (!AcceptHeaderVersionResolver.TryResolve(context.Request.Headers.Accept.ToArray(), out var requestedVersion))
        {
            await WriteUnsupportedVersionProblem(context);

            return;
        }

        var supportedVersions = endpoint.Metadata.GetMetadata<ApiVersionsMetadata>()?.Versions ?? s_defaultVersions;

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
