using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Defra.WasteOrganisations.Api.Tests.Data;

public class PostgresOptionsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private const string HostKey = "Postgres:DefaultHost";
    private const string PasswordKey = "Postgres:Password";
    private const string UseIamAuthenticationKey = "Postgres:UseIamAuthentication";
    private const string HealthCheckFailureStatusKey = "Postgres:HealthCheckFailureStatus";

    [Fact]
    public void WhenValid_ShouldBind()
    {
        var options = Resolve();

        options.DefaultHost.Should().Be("127.0.0.1");
        options.Port.Should().Be(5432);
        options.DatabaseName.Should().Be("waste_organisations");
        options.User.Should().Be("postgres");
        options.UseIamAuthentication.Should().BeFalse();
    }

    [Fact]
    public void WhenHostMissing_ShouldFail()
    {
        var act = () => Resolve(settings => settings.Remove(HostKey));

        act.Should().Throw<OptionsValidationException>().WithMessage("*Host*required*");
    }

    [Fact]
    public void WhenIamOffAndPasswordMissing_ShouldFail()
    {
        var act = () => Resolve(settings => settings.Remove(PasswordKey));

        act.Should().Throw<OptionsValidationException>().WithMessage("*Postgres:Password is required*");
    }

    [Fact]
    public void WhenIamOnAndPasswordMissing_ShouldPass()
    {
        // In CDP the password is a short-lived IAM token, so none is configured
        var options = Resolve(settings =>
        {
            settings.Remove(PasswordKey);
            settings[UseIamAuthenticationKey] = "true";
        });

        options.UseIamAuthentication.Should().BeTrue();
        options.Password.Should().BeNull();
    }

    [Fact]
    public void WhenIamOn_DataSourceShouldVerifyServerCertificateAndHost()
    {
        // The IAM token is sent as the password, so the server must prove who it is:
        // Require would encrypt the connection but accept any certificate
        using var provider = BuildProvider(settings =>
        {
            settings.Remove(PasswordKey);
            settings[UseIamAuthenticationKey] = "true";
        });

        var connectionString = new NpgsqlConnectionStringBuilder(
            provider.GetRequiredService<NpgsqlDataSource>().ConnectionString
        );

        connectionString.SslMode.Should().Be(SslMode.VerifyFull);
    }

    [Fact]
    public void WhenIamOff_DataSourceShouldNotRequireTls()
    {
        using var provider = BuildProvider();

        var connectionString = new NpgsqlConnectionStringBuilder(
            provider.GetRequiredService<NpgsqlDataSource>().ConnectionString
        );

        // The data source hides the password from its connection string, so only the mode is visible
        connectionString.SslMode.Should().Be(SslMode.Prefer);
    }

    [Fact]
    public void WhenHealthCheckFailureStatusNotSet_ShouldDefaultToDegraded()
    {
        // The enum's zero value is Unhealthy, which would fail readiness in any
        // environment that has not set the flag
        var options = Resolve();

        options.HealthCheckFailureStatus.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void WhenHealthCheckFailureStatusIsUnhealthy_ShouldPass()
    {
        var options = Resolve(settings => settings[HealthCheckFailureStatusKey] = "Unhealthy");

        options.HealthCheckFailureStatus.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void WhenHealthCheckFailureStatusIsHealthy_ShouldFail()
    {
        // Healthy would report a failing check as passing
        var act = () => Resolve(settings => settings[HealthCheckFailureStatusKey] = "Healthy");

        act.Should()
            .Throw<OptionsValidationException>()
            .WithMessage("*Postgres:HealthCheckFailureStatus must be Degraded or Unhealthy*");
    }

    [Fact]
    public void WhenHealthCheckFailureStatusIsHealthy_ShouldFailOnStartup()
    {
        // Options are otherwise validated on first use, and nothing in the API reads them
        // before the first request: a bad flag has to stop the deployment, not a later probe
        var builder = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?> { [HealthCheckFailureStatusKey] = "Healthy" }
                    )
            )
        );

        var act = () => builder.CreateClient();

        act.Should().Throw<ObjectDisposedException>();
    }

    private static PostgresOptions Resolve(Action<Dictionary<string, string?>>? configure = null)
    {
        using var provider = BuildProvider(configure);

        return provider.GetRequiredService<IOptions<PostgresOptions>>().Value;
    }

    private static ServiceProvider BuildProvider(Action<Dictionary<string, string?>>? configure = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [HostKey] = "127.0.0.1",
            ["Postgres:DatabaseName"] = "waste_organisations",
            ["Postgres:User"] = "postgres",
            [PasswordKey] = "postgres",
        };
        configure?.Invoke(settings);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection().AddPostgres(configuration).BuildServiceProvider();
    }
}
