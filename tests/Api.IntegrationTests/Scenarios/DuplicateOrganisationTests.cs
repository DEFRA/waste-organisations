using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The v2 create refusing a stored Companies House number (spec 0008), against the Compose stack
public class DuplicateOrganisationTests : IntegrationTestBase
{
    [Fact]
    public async Task SameRequestTwice_ShouldBeConflictAndStoreNothing()
    {
        var body = Ltd(CompaniesHouseNumbers.Generate(), Address(NewSuffix(), "REGISTERED"));

        var first = await PostAsync(body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadOrganisationAsync(first);

        var before = await CountRowsAsync();
        var second = await PostAsync(body);
        var after = await CountRowsAsync();

        await ShouldBeConflictAsync(second, created.Id);
        after.Should().Be(before);
    }

    [Fact]
    public async Task NumberInLowerCaseWithSpaces_ShouldBeConflict()
    {
        var number = CompaniesHouseNumbers.Generate("SC");

        var first = await PostAsync(Ltd(number, Address(NewSuffix(), "REGISTERED")));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadOrganisationAsync(first);

        var second = await PostAsync(Ltd($" {number.ToLowerInvariant()} ", Address(NewSuffix(), "REGISTERED")));

        await ShouldBeConflictAsync(second, created.Id);
    }

    [Fact]
    public async Task SameNumberWithDifferentDetails_ShouldBeConflict()
    {
        var number = CompaniesHouseNumbers.Generate();

        var first = await PostAsync(Ltd(number, Address(NewSuffix(), "REGISTERED")));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadOrganisationAsync(first);

        var second = await PostAsync(Company("LLP", "Other Waste LLP", number, Address(NewSuffix(), "REGISTERED")));
        await ShouldBeConflictAsync(second, created.Id);

        var response = await GetAsync(Testing.Endpoints.Organisations.Get(created.Id));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await ReadDetailsAsync(response);
        read.Name.Should().Be(created.Name);
        read.OrganisationType.Should().Be(created.OrganisationType);
        read.Addresses.Should().ContainSingle().Which.AddressLine1.Should().Be(created.Addresses.Single().AddressLine1);
    }

    [Fact]
    public async Task TwoIdenticalRequestsWithoutANumber_ShouldBothCreate()
    {
        var body = new
        {
            organisation = new
            {
                tradingName = "Sam's Skips",
                regulatorNation = "GB-WLS",
                organisationType = "SOLE_TRADER",
                addresses = new[] { Address(NewSuffix(), "PRINCIPAL") },
            },
        };

        var first = await PostAsync(body);
        var second = await PostAsync(body);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadOrganisationAsync(second)).Id.Should().NotBe((await ReadOrganisationAsync(first)).Id);
    }

    [Fact]
    public async Task TenConcurrentRequestsWithOneNewNumber_ShouldCreateOne()
    {
        var number = CompaniesHouseNumbers.Generate();
        var body = Ltd(number, Address(NewSuffix(), "REGISTERED"));

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PostAsync(body)));

        var created = responses
            .Should()
            .ContainSingle(response => response.StatusCode == HttpStatusCode.Created)
            .Subject;
        var createdId = (await ReadOrganisationAsync(created)).Id;
        var refused = responses.Where(response => response != created).ToArray();
        refused.Should().HaveCount(9);
        foreach (var response in refused)
            await ShouldBeConflictAsync(response, createdId);
        (await CountOrganisationsWithAsync(number)).Should().Be(1);
    }

    private static async Task ShouldBeConflictAsync(HttpResponseMessage response, Guid existingId)
    {
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonNode.Parse(content)!;

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, content);
        response.Headers.Location.Should().Be($"/organisations/{existingId}");
        problem["title"]!.GetValue<string>().Should().Be("Organisation already exists");
        problem["status"]!.GetValue<int>().Should().Be(409);
        problem["detail"]!
            .GetValue<string>()
            .Should()
            .Be("An organisation with this companiesHouseNumber already exists.");
        problem["organisationId"]!.GetValue<Guid>().Should().Be(existingId);
    }

    // Whole-table counts hold still between two requests because the integration tests run in one collection
    private static async Task<(int Organisations, int Links, int Addresses)> CountRowsAsync()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        return (
            await context.Organisations.CountAsync(cancellationToken),
            await context.OrganisationAddresses.CountAsync(cancellationToken),
            await context.Addresses.CountAsync(cancellationToken)
        );
    }

    private static async Task<int> CountOrganisationsWithAsync(string companiesHouseNumber)
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        return await context.Organisations.CountAsync(
            organisation => organisation.CompaniesHouseNumber == companiesHouseNumber,
            TestContext.Current.CancellationToken
        );
    }

    private static object Company(
        string organisationType,
        string name,
        string companiesHouseNumber,
        params object[] addresses
    ) =>
        new
        {
            organisation = new
            {
                name,
                companiesHouseNumber,
                regulatorNation = "GB-ENG",
                organisationType,
                addresses,
            },
        };
}
