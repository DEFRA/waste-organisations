namespace Defra.WasteOrganisations.Api.Versioning;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiVersioning(this IServiceCollection services)
    {
        services.AddTransient<ApiVersioningMiddleware>();
        // Routing applies the policy where an endpoint declares versions, so a path can have one handler per version.
        services.AddSingleton<MatcherPolicy, ApiVersionMatcherPolicy>();

        return services;
    }
}
