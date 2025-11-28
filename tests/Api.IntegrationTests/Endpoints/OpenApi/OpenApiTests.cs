namespace Api.IntegrationTests.Endpoints.OpenApi;

public class OpenApiTests : IntegrationTestBase
{
    [Fact]
    public async Task OpenApi_VerifyAsExpected()
    {
        var client = CreateClient();

        var response = await client.GetStringAsync(Testing.Endpoints.OpenApi.V1, TestContext.Current.CancellationToken);

        await VerifyJson(response);
    }
}
