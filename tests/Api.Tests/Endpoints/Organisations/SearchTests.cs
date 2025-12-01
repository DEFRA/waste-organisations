namespace Api.Tests.Endpoints.Organisations;

public class SearchTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenNoOrganisations_ShouldBeOk()
    {
        var client = CreateClient();

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Search(),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids();
    }
}
