using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class AuthTests : V1TestBase
{
    // Proves both credential helpers produce credentials the API accepts, for API key and OAuth.
    [Theory]
    [Trait("Scenario", "X-AUTH-01")]
    [InlineData(nameof(TestClient.Developer))]
    [InlineData(nameof(TestClient.IntegrationTest))]
    public async Task SearchOrganisations_WithReadScope_ReturnsOk(string clientName)
    {
        using var client = CreateClient(TestClient.Named(clientName));

        var response = await client.GetAsync("/organisations", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
