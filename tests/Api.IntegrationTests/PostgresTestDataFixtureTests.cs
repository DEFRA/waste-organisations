using System.Net;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.IntegrationTests;

public class PostgresTestDataFixtureTests : IntegrationTestBase
{
    private const string Host = "127.0.0.1";

    public static TheoryData<string[]> TagsWithoutBaseline => new([], ["v1.1.0"]);

    [Fact]
    public void EnsureComposeDatabase_WithTheComposeDatabaseAndBaselineTag_ShouldNotThrow()
    {
        var act = () =>
            PostgresTestDataFixture.EnsureComposeDatabase("waste_organisations", Host, ["v1.0.0", "v1.1.0"]);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureComposeDatabase_WithAnotherDatabaseName_ShouldThrow()
    {
        var act = () => PostgresTestDataFixture.EnsureComposeDatabase("my_own_database", Host, ["v1.0.0"]);

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().Contain("my_own_database").And.Contain(Host).And.Contain("waste_organisations");
    }

    [Theory]
    [MemberData(nameof(TagsWithoutBaseline))]
    public void EnsureComposeDatabase_WithoutTheBaselineTag_ShouldThrow(string[] tags)
    {
        var act = () => PostgresTestDataFixture.EnsureComposeDatabase("waste_organisations", Host, tags);

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().Contain("waste_organisations").And.Contain(Host).And.Contain("v1.0.0");
    }

    [Fact]
    public async Task ClearAsync_AfterAnOrganisationIsStored_ShouldEmptyTestTablesAndKeepSeededRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var body = new
        {
            organisation = new
            {
                name = "Acme Waste Ltd",
                companiesHouseNumber = CompaniesHouseNumbers.Generate(),
                regulatorNation = "GB-ENG",
                organisationType = "LTD",
                addresses = new[] { Address(NewSuffix(), "REGISTERED") },
            },
        };
        var response = await PostAsync(body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var seededBefore = await CountSeededRowsAsync(context, cancellationToken);
        (await context.Organisations.AnyAsync(cancellationToken)).Should().BeTrue();

        await PostgresTestDataFixture.ClearAsync(context, cancellationToken);

        (await CountSeededRowsAsync(context, cancellationToken)).Should().Be(seededBefore);
        (await context.Organisations.AnyAsync(cancellationToken)).Should().BeFalse();
        (await context.OrganisationAddresses.AnyAsync(cancellationToken)).Should().BeFalse();
        (await context.Addresses.AnyAsync(cancellationToken)).Should().BeFalse();
    }

    private static async Task<(int OrganisationTypes, int AddressUsageTypes, int Changesets)> CountSeededRowsAsync(
        PostgresDbContext context,
        CancellationToken cancellationToken
    ) =>
        (
            await context.OrganisationTypes.CountAsync(cancellationToken),
            await context.AddressUsageTypes.CountAsync(cancellationToken),
            await context
                .Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM databasechangelog")
                .SingleAsync(cancellationToken)
        );
}
