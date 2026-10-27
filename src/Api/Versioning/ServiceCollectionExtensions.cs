namespace Defra.WasteOrganisations.Api.Versioning;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiVersioning(this IServiceCollection services)
    {
        services.AddTransient<ApiVersioningMiddleware>();

        return services;
    }
}
