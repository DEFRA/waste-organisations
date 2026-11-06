using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.Tests.Endpoints.OpenApi;

public class ReDocTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task Index_ShouldListBothDocuments()
    {
        var client = CreateClient(addAuthorizationHeader: false);

        using var response = await client.GetAsync("redoc/index.html", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The custom page, not the package default: one ReDoc page with a dropdown over the documents
        // set by Program.cs, and every template placeholder substituted by the middleware
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        html.Should().Contain("<select id=\"version-select\">");
        html.Should().Contain("window.openApiDocuments = [");
        html.Should().Contain($"\"/{Testing.Endpoints.OpenApi.V1}\"");
        html.Should().Contain($"\"/{Testing.Endpoints.OpenApi.V2}\"");
        html.Should().Contain("<script src=\"redoc.standalone.js\"></script>");
        html.Should().NotContain("%(");
    }
}
