using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

public class DatabaseChangelogTests : IntegrationTestBase
{
    // This resolves the context from AddPostgres and queries through it, connecting from the host
    // to the Compose Postgres the way the API does under dotnet run. The five organisation entities
    // are the ones PostgresDbContext maps onto the Liquibase changelog tables
    [Fact]
    public async Task PostgresDbContext_ShouldConnectAndMapFiveEntities()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var canConnect = await context.Database.CanConnectAsync(TestContext.Current.CancellationToken);

        canConnect.Should().BeTrue();
        context.Model.GetEntityTypes().Should().HaveCount(5);
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

    // The v1.1.0 tag is the last changeset of db.changelog-1.1.xml, so finding it proves the
    // organisation schema changelog applied in full
    [Fact]
    public async Task DatabaseChangelog_ShouldHoldOrganisationSchemaTag()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var tags = await context
            .Database.SqlQueryRaw<string>("SELECT tag AS \"Value\" FROM databasechangelog WHERE tag IS NOT NULL")
            .ToListAsync(TestContext.Current.CancellationToken);

        tags.Should().Contain("v1.1.0");
    }

    // The changelog seeds address_usagetype with the board's six usage types and nothing else
    [Fact]
    public async Task AddressUsageType_ShouldHoldTheSixSeededValues()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var usageTypes = await context
            .AddressUsageTypes.Select(u => u.UsageType)
            .ToListAsync(TestContext.Current.CancellationToken);

        usageTypes
            .Should()
            .BeEquivalentTo("Registered", "Audit", "ServiceOfNotice", "Principal", "Organisation", "Site");
    }

    private static ServiceProvider BuildServiceProvider()
    {
        // The Postgres section of src/Api/appsettings.Development.json, which compose.yml matches
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Postgres:DefaultHost"] = "127.0.0.1",
                    ["Postgres:Port"] = "5432",
                    ["Postgres:DatabaseName"] = "waste_organisations",
                    ["Postgres:User"] = "postgres",
                    ["Postgres:Password"] = "postgres",
                    ["Postgres:UseIamAuthentication"] = "false",
                }
            )
            .Build();

        return new ServiceCollection().AddPostgres(configuration).BuildServiceProvider();
    }
}
