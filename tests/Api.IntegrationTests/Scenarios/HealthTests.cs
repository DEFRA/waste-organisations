using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

public class HealthTests : IntegrationTestBase
{
    // In-process (Api.Tests) nothing listens on 5432 and the check reports Degraded, so this is
    // the only test that proves the API in Compose can reach Postgres through its shared data source
    [Fact]
    public async Task HealthAll_ShouldReportPostgresHealthy()
    {
        var client = CreateClient();

        var response = await client.GetAsync(Testing.Endpoints.Health.All(), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
