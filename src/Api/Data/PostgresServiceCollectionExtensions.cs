using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Defra.WasteOrganisations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(
        this IServiceCollection services,
        IConfiguration configuration,
        bool integrationTest
    )
    {
        services.AddSingleton<IValidateOptions<PostgresOptions>, PostgresOptionsValidator>();
        services
            .AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
