using System.Net;
using AwesomeAssertions;

namespace Api.Tests.Endpoints.Health;

public class HealthTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task Health_ShouldBeOk()
    {
        var client = CreateClient();

        var response = await client.GetAsync(Testing.Endpoints.Health.Ready(), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
