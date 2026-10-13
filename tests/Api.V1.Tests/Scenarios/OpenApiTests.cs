using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class OpenApiTests : V1TestBase
{
    // The snapshot pins the published contract of the five v1 endpoints: paths, methods,
    // parameters, schemas and enum values. Any change to it is a change clients can see.
    [Fact]
    [Trait("Scenario", "X-CONTRACT-01")]
    public async Task OpenApiDocument_WithoutCredentials_MatchesSnapshot()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // `servers` echoes the address the request was sent to, so it is scrubbed to keep the
        // snapshot valid when the suite runs against another host.
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .AddScrubber(text => text.Replace(BaseAddress.ToString(), "{BaseAddress}"));
    }
}
