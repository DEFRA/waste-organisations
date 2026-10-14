using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

public class DatabaseChangelogTests : IntegrationTestBase
{
    // Nothing in the API uses PostgresDbContext yet, so this is the only test that resolves the
    // context from AddPostgres and queries through it. It connects from the host to the Compose
    // Postgres, the way the API does under dotnet run
    [Fact]
    public async Task PostgresDbContext_ShouldConnectAndHaveNoEntities()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var canConnect = await context.Database.CanConnectAsync(TestContext.Current.CancellationToken);

        canConnect.Should().BeTrue();
        // The schema belongs to the Liquibase changelog; entities arrive with the stories that need them
        context.Model.GetEntityTypes().Should().BeEmpty();
    }

    // The v1.0.0 tag is the only thing the baseline changeset writes, so finding it proves the
    // liquibase service in Compose applied changelog/ before the API started
    [Fact]
    public async Task DatabaseChangelog_ShouldHoldBaselineTag()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var tags = await context
            .Database.SqlQueryRaw<string>("SELECT tag AS \"Value\" FROM databasechangelog WHERE tag IS NOT NULL")
            .ToListAsync(TestContext.Current.CancellationToken);

        tags.Should().Contain("v1.0.0");
    }

    private static ServiceProvider BuildServiceProvider()
    {
        // The Postgres section of src/Api/appsettings.Development.json, which compose.yml matches
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Postgres:Host"] = "127.0.0.1",
                    ["Postgres:Port"] = "5432",
                    ["Postgres:DatabaseName"] = "waste-organisations",
                    ["Postgres:User"] = "postgres",
                    ["Postgres:Password"] = "postgres",
                    ["Postgres:UseIamAuthentication"] = "false",
                }
            )
            .Build();

        return new ServiceCollection().AddPostgres(configuration).BuildServiceProvider();
    }
}
