using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class AuthTests : V1TestBase
{
    // Proves the credential helpers produce credentials the API accepts, for API key and OAuth.
    [Theory]
    [Trait("Scenario", "X-AUTH-01")]
    [InlineData(nameof(TestClient.Developer))]
    [InlineData(nameof(TestClient.IntegrationTest))]
    [InlineData(nameof(TestClient.ReadOnlyApiKey))]
    [InlineData(nameof(TestClient.ReadOnlyOAuth))]
    public async Task SearchOrganisations_WithReadScope_ReturnsOk(string clientName)
    {
        using var client = CreateClient(TestClient.Named(clientName));

        var response = await client.GetAsync("/organisations", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Proves the write-only clients in compose.yml authenticate but lack the read scope.
    [Theory]
    [Trait("Scenario", "X-AUTH-01")]
    [InlineData(nameof(TestClient.WriteOnlyApiKey))]
    [InlineData(nameof(TestClient.WriteOnlyOAuth))]
    public async Task SearchOrganisations_WithoutReadScope_ReturnsForbidden(string clientName)
    {
        using var client = CreateClient(TestClient.Named(clientName));

        var response = await client.GetAsync("/organisations", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
