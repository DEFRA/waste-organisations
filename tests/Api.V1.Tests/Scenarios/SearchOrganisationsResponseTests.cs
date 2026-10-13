using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// The shape a client parses from a search: one <c>organisations</c> array whose entries carry the
/// same fields and wire values as a get, so a change to either shows up as a snapshot diff.
/// </summary>
public class SearchOrganisationsResponseTests : V1TestBase
{
    // The search returns every matching organisation in the shared stack, so only this test's entry
    // is snapshotted. A second, cancelled registration puts both statuses and a second timestamp pair
    // on the wire, in the order the registrations were added.
    [Fact]
    [Trait("Scenario", "ORG-SEARCH-16")]
    public async Task Search_ReturnsOrganisationsArrayWithEachEntryInGetShape()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        await PutRegistration(id, "EXPORTER-2030", "CANCELLED");
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync(
            "/organisations?registrations=EXPORTER&registrationYears=2030&statuses=CANCELLED",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode
            .Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!
            .AsObject();
        body.Select(p => p.Key).Should().Equal("organisations");
        var entry = body["organisations"]!.AsArray().Single(o => o!["id"]!.GetValue<Guid>() == id)!;
        await VerifyJson(entry.ToJsonString());
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
}
