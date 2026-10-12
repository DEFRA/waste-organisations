using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

// The stack is shared and never reset, so search results also hold organisations from other tests
// and earlier runs. Every assertion is therefore about the ids the test itself created.
public class SearchOrganisationsTests : V1TestBase
{
    private static readonly string[] AllTypes =
    [
        "SMALL_PRODUCER",
        "LARGE_PRODUCER",
        "COMPLIANCE_SCHEME",
        "REPROCESSOR",
        "EXPORTER",
    ];

    // Each search entry must be the same organisation a get returns, registrations included.
    [Fact]
    [Trait("Scenario", "ORG-SEARCH-02")]
    public async Task Search_WhenNoFilters_ReturnsEveryOrganisationWithAllItsRegistrations()
    {
        var withOne = await CreateOrganisation(OrganisationPayloads.Full());
        var withTwo = await CreateOrganisation(OrganisationPayloads.Minimal(type: "EXPORTER"));
        await PutRegistration(withTwo, "REPROCESSOR-2030", "CANCELLED");
        var withCancelled = await CreateOrganisation(OrganisationPayloads.Full(status: "CANCELLED"));

        var results = await Search("/organisations");

        foreach (var id in new[] { withOne, withTwo, withCancelled })
        {
            results.Should().ContainKey(id);
            JsonNode.DeepEquals(results[id], await GetOrganisation(id)).Should().BeTrue($"{id} must match its get");
        }
        results[withTwo]["registrations"]!.AsArray().Should().HaveCount(2);
    }

    [Theory]
    [Trait("Scenario", "ORG-SEARCH-03")]
    [InlineData("SMALL_PRODUCER")]
    [InlineData("LARGE_PRODUCER")]
    [InlineData("COMPLIANCE_SCHEME")]
    [InlineData("REPROCESSOR")]
    [InlineData("EXPORTER")]
    public async Task Search_WhenFilteredByOneType_ReturnsOnlyOrganisationsWithThatType(string type)
    {
        var idsByType = new Dictionary<string, Guid>();
        foreach (var each in AllTypes)
            idsByType[each] = await CreateOrganisation(OrganisationPayloads.Full(type: each));

        var results = await Search($"/organisations?registrations={type}");

        Returned(results, idsByType).Should().BeEquivalentTo(type);
    }

    [Fact]
    [Trait("Scenario", "ORG-SEARCH-04")]
    public async Task Search_WhenSeveralValuesInOneParameter_ReturnsOrganisationsMatchingAnyOfThem()
    {
        var idsByType = new Dictionary<string, Guid>();
        foreach (var each in new[] { "SMALL_PRODUCER", "EXPORTER", "REPROCESSOR" })
            idsByType[each] = await CreateOrganisation(OrganisationPayloads.Full(type: each));

        var results = await Search("/organisations?registrations=SMALL_PRODUCER,EXPORTER");

        Returned(results, idsByType).Should().BeEquivalentTo("SMALL_PRODUCER", "EXPORTER");
    }

    // 2023 and 2050 are the first and last years the API accepts.
    [Theory]
    [Trait("Scenario", "ORG-SEARCH-05")]
    [InlineData("2023", new[] { 2023 })]
    [InlineData("2050", new[] { 2050 })]
    [InlineData("2023,2050", new[] { 2023, 2050 })]
    public async Task Search_WhenFilteredByYears_ReturnsOrganisationsWithARegistrationInAnyListedYear(
        string years,
        int[] expected
    )
    {
        var idsByYear = new Dictionary<int, Guid>();
        foreach (var year in new[] { 2023, 2025, 2050 })
            idsByYear[year] = await CreateOrganisation(OrganisationPayloads.Full(registrationYear: year));

        var results = await Search($"/organisations?registrationYears={years}");

        Returned(results, idsByYear).Should().BeEquivalentTo(expected);
    }

    [Theory]
    [Trait("Scenario", "ORG-SEARCH-06")]
    [InlineData("REGISTERED", new[] { "REGISTERED" })]
    [InlineData("CANCELLED", new[] { "CANCELLED" })]
    [InlineData("REGISTERED,CANCELLED", new[] { "REGISTERED", "CANCELLED" })]
    public async Task Search_WhenFilteredByStatuses_ReturnsOrganisationsWithARegistrationInAnyListedStatus(
        string statuses,
        string[] expected
    )
    {
        var idsByStatus = new Dictionary<string, Guid>();
        foreach (var status in new[] { "REGISTERED", "CANCELLED" })
            idsByStatus[status] = await CreateOrganisation(OrganisationPayloads.Full(status: status));

        var results = await Search($"/organisations?statuses={statuses}");

        Returned(results, idsByStatus).Should().BeEquivalentTo(expected);
    }

    // The organisation holds EXPORTER-2023 REGISTERED and REPROCESSOR-2050 CANCELLED. All filters
    // must be met by one registration, so mixing values from the two never matches. The last query
    // takes every value from one registration and shows the organisation can be found at all.
    [Theory]
    [Trait("Scenario", "ORG-SEARCH-07")]
    [InlineData("registrations=EXPORTER&statuses=CANCELLED")]
    [InlineData("registrations=REPROCESSOR&registrationYears=2023")]
    [InlineData("registrationYears=2050&statuses=REGISTERED")]
    public async Task Search_WhenFiltersAreMetByDifferentRegistrations_DoesNotReturnTheOrganisation(string filters)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full(type: "EXPORTER", registrationYear: 2023));
        await PutRegistration(id, "REPROCESSOR-2050", "CANCELLED");

        var results = await Search($"/organisations?{filters}");

        results.Should().NotContainKey(id);
        (await Search("/organisations?registrations=REPROCESSOR&registrationYears=2050&statuses=CANCELLED"))
            .Should()
            .ContainKey(id);
    }

    [Fact]
    [Trait("Scenario", "ORG-SEARCH-08")]
    public async Task Search_WhenMatchedThroughOneRegistration_ReturnsAllItsRegistrations()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full(type: "EXPORTER", registrationYear: 2023));
        await PutRegistration(id, "REPROCESSOR-2050", "CANCELLED");

        var results = await Search("/organisations?registrations=EXPORTER");

        results.Should().ContainKey(id);
        results[id]["registrations"]!
            .AsArray()
            .Select(r => $"{r!["type"]}-{r["registrationYear"]} {r["status"]}")
            .Should()
            .BeEquivalentTo("EXPORTER-2023 REGISTERED", "REPROCESSOR-2050 CANCELLED");
        JsonNode.DeepEquals(results[id], await GetOrganisation(id)).Should().BeTrue();
    }

    // Both organisations hold only SMALL_PRODUCER-2025 REGISTERED; organisations from other tests
    // may match, so the check is only that neither of these two is returned.
    [Theory]
    [Trait("Scenario", "ORG-SEARCH-09")]
    [InlineData("registrations=EXPORTER")]
    [InlineData("registrationYears=2050")]
    [InlineData("statuses=CANCELLED")]
    [InlineData("registrations=SMALL_PRODUCER&registrationYears=2026")]
    public async Task Search_WhenNoCreatedOrganisationMatches_ReturnsOkWithoutThem(string filters)
    {
        var first = await CreateOrganisation(OrganisationPayloads.Full());
        var second = await CreateOrganisation(OrganisationPayloads.Minimal());

        var results = await Search($"/organisations?{filters}");

        results.Should().NotContainKey(first).And.NotContainKey(second);
    }

    // REG-DEL-05 removes a single registration; here the organisation had two, so the search
    // must stop finding it only once the last of them is gone.
    [Fact]
    [Trait("Scenario", "ORG-SEARCH-10")]
    public async Task Search_WhenLastRegistrationDeleted_NeverReturnsTheOrganisationButGetStillDoes()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        await PutRegistration(id, "EXPORTER-2030", "CANCELLED");
        await DeleteRegistration(id, "SMALL_PRODUCER-2025");
        (await Search("/organisations")).Should().ContainKey(id);

        await DeleteRegistration(id, "EXPORTER-2030");

        foreach (
            var query in new[]
            {
                "/organisations",
                "/organisations?registrations=EXPORTER",
                "/organisations?registrations=SMALL_PRODUCER",
                "/organisations?registrationYears=2025,2030",
                "/organisations?statuses=REGISTERED,CANCELLED",
            }
        )
            (await Search(query)).Should().NotContainKey(id, $"\"{query}\" must not return it");
        var organisation = await GetOrganisation(id);
        organisation["id"]!.GetValue<Guid>().Should().Be(id);
        organisation["registrations"]!.AsArray().Should().BeEmpty();
    }

    // The keys of the organisations this test created that the search returned.
    private static IEnumerable<TKey> Returned<TKey>(Dictionary<Guid, JsonObject> results, Dictionary<TKey, Guid> ids)
        where TKey : notnull => ids.Where(p => results.ContainsKey(p.Value)).Select(p => p.Key);

    private static async Task<Dictionary<Guid, JsonObject>> Search(string path)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        return body["organisations"]!.AsArray().Select(o => o!.AsObject()).ToDictionary(o => o["id"]!.GetValue<Guid>());
    }

    private static async Task<JsonObject> GetOrganisation(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode
            .Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!
            .AsObject();
    }

    private static async Task PutRegistration(Guid id, string key, string status)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.PutAsync(
            $"/organisations/{id}/registrations/{key}",
            Json($$"""{ "status": "{{status}}" }"""),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task DeleteRegistration(Guid id, string key)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.DeleteAsync(
            $"/organisations/{id}/registrations/{key}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
