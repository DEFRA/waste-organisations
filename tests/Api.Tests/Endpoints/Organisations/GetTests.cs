using System.Net;
using AwesomeAssertions;

namespace Api.Tests.Endpoints.Organisations;

public class GetTests(TestWebApplicationFactory<Program> factory) : IClassFixture<TestWebApplicationFactory<Program>>
{
    [Fact]
    public async Task WhenOrganisationFound_ShouldBeOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Get("b6f76437-65b6-4ed2-a7d5-c50e9af76201"),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids();
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(Guid.Empty.ToString()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
