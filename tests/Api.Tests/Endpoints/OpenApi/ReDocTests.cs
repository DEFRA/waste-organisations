using System.Net;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Swashbuckle.AspNetCore.ReDoc;

namespace Defra.WasteOrganisations.Api.Tests.Endpoints.OpenApi;

public partial class ReDocTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
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
        html.Should().Contain("import { init } from \"./redoc.standalone.js\";");
        html.Should().NotContain("%(");
    }

    [Fact]
    public async Task Bundle_ShouldExportTheInitFunctionThePageImports()
    {
        var client = CreateClient(addAuthorizationHeader: false);

        using var response = await client.GetAsync("redoc/redoc.standalone.js", TestContext.Current.CancellationToken);
        var bundle = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // No test in this project runs the page's script (ReDocPageTests in Api.IntegrationTests does), so this
        // pins what the script relies on: the bundle the package serves exports init. The page rendered nothing
        // when a package update replaced a global with this export
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ExportsInit().IsMatch(bundle).Should().BeTrue("the page imports init from the bundle");
    }

    [Theory]
    [InlineData("index.html")]
    [InlineData("index.js")]
    public async Task PackageTemplate_ShouldBeTheOneThePageWasCopiedFrom(string file)
    {
        // The page in src/Api/Documentation/ReDoc is a copy of the package's default template. A package update
        // that changes the template fails here, so the copy is brought in line before the update is merged
        await using var stream = typeof(ReDocOptions).Assembly.GetManifestResourceStream(
            $"Swashbuckle.AspNetCore.ReDoc.{file}"
        );
        stream.Should().NotBeNull("the package embeds its default template");
        using var reader = new StreamReader(stream);
        var template = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        await Verify(template).UseParameters(file);
    }

    [GeneratedRegex(@"export\s*\{[^}]*\binit\s*[,}]")]
    private static partial Regex ExportsInit();
}
