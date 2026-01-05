using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace Defra.WasteOrganisations.Api.Tests.Endpoints.Security;

public class SecurityTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    // HSTS header should not normally be added for non-HTTPS requests, which is always the case
    // currently for a Dockerized application in CDP.
    // To address this, we add it explicitly instead of calling UseHsts().
    [Fact]
    public async Task WhenResponding_StrictTransportSecurityHeadersReturned()
    {
        var client = CreateClient();

        var response = await client.GetAsync(Testing.Endpoints.OpenApi.V1, TestContext.Current.CancellationToken);

        Assert.Contains(response.Headers, h => h.Key == HeaderNames.StrictTransportSecurity);

        var values = response.Headers.GetValues(HeaderNames.StrictTransportSecurity);
        Assert.Contains(values, v => v.Contains("max-age=31536000")); // 365 days
        await Verify(response).IgnoreMember("Authorization");
    }
}
