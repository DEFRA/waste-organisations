using System.Diagnostics.CodeAnalysis;

namespace Defra.WasteOrganisations.Api.Utils.Health;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services.AddHealthChecks();

        return services;
    }
}
