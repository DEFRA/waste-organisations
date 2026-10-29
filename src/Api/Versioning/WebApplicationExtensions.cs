namespace Defra.WasteOrganisations.Api.Versioning;

public static class WebApplicationExtensions
{
    public static IApplicationBuilder UseApiVersioning(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiVersioningMiddleware>();
}
