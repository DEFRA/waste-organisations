namespace Defra.WasteOrganisations.Api.Tests.Endpoints.OpenApi;

public class OpenApiTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task OpenApi_VerifyAsExpected()
    {
        var client = CreateClient();

        var response = await client.GetStringAsync(
            Defra.WasteOrganisations.Testing.Endpoints.OpenApi.V1,
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response);
    }
}
