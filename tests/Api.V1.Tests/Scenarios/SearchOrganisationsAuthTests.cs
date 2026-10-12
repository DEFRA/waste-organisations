using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// Which callers the search lets through: it needs the read scope, and a client with only the write
/// scope is refused rather than unauthenticated.
/// </summary>
public class SearchOrganisationsAuthTests : V1TestBase
{
    // An organisation exists in every case, so a permitted search is also checked to return it
    // rather than only to answer 200.
    [Theory]
    [Trait("Scenario", "ORG-SEARCH-17")]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(nameof(TestClient.WriteOnlyApiKey), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.WriteOnlyOAuth), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.ReadOnlyApiKey), HttpStatusCode.OK)]
    [InlineData(nameof(TestClient.ReadOnlyOAuth), HttpStatusCode.OK)]
    public async Task Search_ByClientScope_ReturnsExpectedStatus(string? clientName, HttpStatusCode expected)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        using var response = await client.GetAsync("/organisations", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(expected);
        if (expected != HttpStatusCode.OK)
            return;
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        body["organisations"]!.AsArray().Select(o => o!["id"]!.GetValue<Guid>()).Should().Contain(id);
    }
}
