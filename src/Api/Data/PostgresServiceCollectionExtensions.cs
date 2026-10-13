using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.RDS.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Defra.WasteOrganisations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public static class PostgresServiceCollectionExtensions
{
    // An RDS auth token lives 15 minutes, so it is replaced well before it expires
    private static readonly TimeSpan s_iamTokenRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan s_iamTokenFailureRetryInterval = TimeSpan.FromSeconds(30);

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

        if (integrationTest)
            return services;

        // One data source for the whole application, so every consumer shares its pool and,
        // in CDP, its IAM token. Building it opens no connection: the API starts whether or
        // not Postgres is reachable
        services.AddSingleton(sp => CreateDataSource(sp.GetRequiredService<IOptions<PostgresOptions>>().Value));
        services.AddDbContext<PostgresDbContext>(
            (sp, options) =>
                options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.EnableRetryOnFailure())
        );

        return services;
    }

    private static NpgsqlDataSource CreateDataSource(PostgresOptions options)
    {
        var builder = new NpgsqlDataSourceBuilder
        {
            ConnectionStringBuilder =
            {
                Host = options.Host,
                Port = options.Port,
                Database = options.DatabaseName,
                Username = options.User,
            },
        };

        if (options.UseIamAuthentication)
        {
            // RDS only accepts an IAM token over TLS
            builder.ConnectionStringBuilder.SslMode = SslMode.Require;

            // The token is signed locally with the default AWS credential chain (the ECS task
            // role in CDP); generating it makes no call to AWS
            builder.UsePeriodicPasswordProvider(
                (settings, _) =>
                    ValueTask.FromResult(
                        RDSAuthTokenGenerator.GenerateAuthToken(
                            RegionEndpoint.EUWest2,
                            settings.Host,
                            settings.Port,
                            settings.Username
                        )
                    ),
                s_iamTokenRefreshInterval,
                s_iamTokenFailureRetryInterval
            );
        }
        else
        {
            builder.ConnectionStringBuilder.Password = options.Password;
        }

        return builder.Build();
    }
}
