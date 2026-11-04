namespace Defra.WasteOrganisations.Api.Tests.Endpoints.OpenApi;

public class OpenApiTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task OpenApi_VerifyAsExpected()
    {
        var client = CreateClient();

        var response = await client.GetStringAsync(Testing.Endpoints.OpenApi.V1, TestContext.Current.CancellationToken);

        await VerifyJson(response);
    }

    [Fact]
    public async Task OpenApiV2_VerifyAsExpected()
    {
        var client = CreateClient();

        var response = await client.GetStringAsync(Testing.Endpoints.OpenApi.V2, TestContext.Current.CancellationToken);

        await VerifyJson(response);
    }
}
