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
}
