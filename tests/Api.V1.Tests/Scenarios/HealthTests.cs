using System.Net;
using System.Text.Json;
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
    [Trait("Scenario", "X-OPS-02")]
    public async Task Redoc_WithoutCredentials_ReturnsHtml()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/redoc/index.html", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    [Trait("Scenario", "X-OPS-03")]
    public async Task HealthAll_WithoutCredentials_ReturnsHealthyJson()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/health/all", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // Only the overall status is pinned: the per-check results depend on which checks the
        // deployment registers, which is not part of the v1 contract.
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        body.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    [Trait("Scenario", "X-OPS-03")]
    public async Task HealthAuthorized_WithoutCredentials_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/health/authorized", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Any valid API-key client passes, whatever its scopes: the endpoint asks only for an
    // authenticated caller, not for the read or write policy.
    [Theory]
    [Trait("Scenario", "X-OPS-03")]
    [InlineData(nameof(TestClient.ReadOnlyApiKey))]
    [InlineData(nameof(TestClient.WriteOnlyApiKey))]
    public async Task HealthAuthorized_WithLimitedScopeApiKeyClient_ReturnsHealthy(string testClient)
    {
        using var client = CreateClient(TestClient.Named(testClient));

        using var response = await client.GetAsync("/health/authorized", TestContext.Current.CancellationToken);

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

    // Characterisation: documents current behaviour, not a requirement
    // Same cause as the full-scope OAuth case above: only the Basic scheme is tried.
    [Theory]
    [Trait("Scenario", "X-OPS-03")]
    [InlineData(nameof(TestClient.ReadOnlyOAuth))]
    [InlineData(nameof(TestClient.WriteOnlyOAuth))]
    public async Task HealthAuthorized_WithLimitedScopeOAuthClient_ReturnsUnauthorized(string testClient)
    {
        using var client = CreateClient(TestClient.Named(testClient));

        using var response = await client.GetAsync("/health/authorized", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
