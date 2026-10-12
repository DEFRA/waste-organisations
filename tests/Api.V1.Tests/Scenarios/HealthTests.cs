using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class HealthTests : V1TestBase
{
    [Fact]
    [Trait("Scenario", "X-OPS-01")]
    public async Task Health_WithoutCredentials_ReturnsHealthy()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    [Trait("Scenario", "X-OPS-03")]
    public async Task HealthAuthorized_WithApiKeyClient_ReturnsHealthy()
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.GetAsync("/health/authorized", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    // Characterisation: documents current behaviour, not a requirement
    // The endpoint uses the default authorization policy, which authenticates with the Basic
    // scheme only, so a valid OAuth client is turned away. Likely bug.
    [Fact]
    [Trait("Scenario", "X-OPS-03")]
    public async Task HealthAuthorized_WithOAuthClient_ReturnsUnauthorized()
    {
        using var client = CreateClient(TestClient.IntegrationTest);

        var response = await client.GetAsync("/health/authorized", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
