using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres;
using Microsoft.EntityFrameworkCore;
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

    // Each tag is the last changeset of its changelog, so finding it proves that changelog applied
    // in full through the liquibase service in Compose:
    // - v1.0.0: the baseline, applied before the API started
    // - v1.1.0: the organisation schema
    // - v1.2.0: the v2 create schema (shared addresses, the ten organisation types)
    // - v1.3.0: the GDS address columns and the fourteen-column ux_address_values
    // - v1.4.0: the GDS checks and the six-column ux_address_values
    [Theory]
    [InlineData("v1.0.0")]
    [InlineData("v1.1.0")]
    [InlineData("v1.2.0")]
    [InlineData("v1.3.0")]
    [InlineData("v1.4.0")]
    public async Task DatabaseChangelog_ShouldHoldTag(string tag)
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var tags = await context
            .Database.SqlQueryRaw<string>("SELECT tag AS \"Value\" FROM databasechangelog WHERE tag IS NOT NULL")
            .ToListAsync(TestContext.Current.CancellationToken);

        tags.Should().Contain(tag);
    }

    // The changelog seeds organisation_type with exactly the codes the API publishes, so the
    // contract and the table cannot drift apart
    [Fact]
    public async Task OrganisationType_ShouldHoldThePublishedCodes()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var codes = await context
            .OrganisationTypes.Select(t => t.Code)
            .ToListAsync(TestContext.Current.CancellationToken);

        codes.Should().BeEquivalentTo(OrganisationTypeCodes.All.Select(c => c.Code));
    }

    // The changelog seeds address_usagetype with the five kinds of address on the API page and nothing
    // else; 1.2 renamed them to the upper-case values the v2 addressTypes carry
    [Fact]
    public async Task AddressUsageType_ShouldHoldTheFiveSeededValues()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var usageTypes = await context
            .AddressUsageTypes.Select(u => u.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        usageTypes.Should().BeEquivalentTo("REGISTERED", "AUDIT", "SERVICE_OF_NOTICE", "PRINCIPAL", "CORRESPONDENCE");
    }
}
