using System.Net;
using AwesomeAssertions;

namespace Api.Tests.Endpoints.Organisations;

public class GetTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenOrganisationFound_ShouldBeOk()
    {
        var client = CreateClient();

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Get("b6f76437-65b6-4ed2-a7d5-c50e9af76201"),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids();
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(Guid.Empty.ToString()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
