using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.RDS.Util;
using Defra.WasteOrganisations.Api.Utils.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Defra.WasteOrganisations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public static class PostgresServiceCollectionExtensions
{
    // An RDS auth token lives 15 minutes, so it is replaced well before it expires
    private static readonly TimeSpan s_iamTokenRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan s_iamTokenFailureRetryInterval = TimeSpan.FromSeconds(30);

    // Shorter than the Npgsql connect timeout (15 seconds), so a connection that hangs reports
    // the configured failure status instead of holding the readiness probe open
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(3);

    public static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<PostgresOptions>, PostgresOptionsValidator>();
        services
            .AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // One data source for the whole application, so every consumer shares its pool and,
        // in CDP, its IAM token. Building it opens no connection: the API starts whether or
        // not Postgres is reachable, and the in-process tests need no special case, only the
        // Degraded failure status their settings file sets
        services.AddSingleton(sp => CreateDataSource(sp.GetRequiredService<IOptions<PostgresOptions>>().Value));
        services.AddDbContext<PostgresDbContext>(
            (sp, options) =>
                options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.EnableRetryOnFailure())
        );

        // The failure status decides whether an unreachable Postgres takes the instance out of
        // service (Unhealthy) or only shows in /health/all and the logs (Degraded). A check is
        // registered before options can be resolved, so it is read from configuration here;
        // ValidateOnStart above still rejects an invalid value
        var healthCheckFailureStatus =
            configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>()?.HealthCheckFailureStatus
            ?? HealthStatus.Degraded;

        services
            .AddHealthChecks()
            .AddNpgSql(
                sp => sp.GetRequiredService<NpgsqlDataSource>(),
                name: "postgres",
                failureStatus: healthCheckFailureStatus,
                tags: [WebApplicationExtensions.Ready],
                timeout: s_healthCheckTimeout
            );

        return services;
    }

    private static NpgsqlDataSource CreateDataSource(PostgresOptions options)
    {
        var builder = new NpgsqlDataSourceBuilder
        {
            ConnectionStringBuilder =
            {
                Host = options.DefaultHost,
                Port = options.Port,
                Database = options.DatabaseName,
                Username = options.User,
            },
        };

        if (options.UseIamAuthentication)
        {
            // RDS only accepts an IAM token over TLS. VerifyFull checks the certificate chain
            // and the host name: Require would encrypt but accept any server, and the token is
            // a credential worth protecting. The RDS CA comes from the trust store CDP injects
            builder.ConnectionStringBuilder.SslMode = SslMode.VerifyFull;

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
