namespace Api.Tests.Endpoints.OpenApi;

public class OpenApiTests(TestWebApplicationFactory<Program> factory)
    : IClassFixture<TestWebApplicationFactory<Program>>
{
    [Fact]
    public async Task OpenApi_VerifyAsExpected()
    {
        var client = factory.CreateClient();

        var response = await client.GetStringAsync(Testing.Endpoints.OpenApi.V1, TestContext.Current.CancellationToken);

        await VerifyJson(response);
    }
}
