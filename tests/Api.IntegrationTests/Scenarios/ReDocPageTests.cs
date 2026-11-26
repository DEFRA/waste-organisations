using AwesomeAssertions;
using Microsoft.Playwright;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The page's script only runs in a browser. A package update once left it rendering nothing while every
// HTTP-level test passed, so this loads the page from the API in Compose in headless Chrome
public class ReDocPageTests : IntegrationTestBase
{
    private const string ApiOrigin = "http://localhost:8085";
    private const float RenderTimeoutMilliseconds = 30_000;

    [Theory]
    [InlineData("/redoc/index.html", "Waste Organisations REST API (1.0.0)")]
    [InlineData("/redoc/index.html?spec=v2", "Waste Organisations REST API (2.0.0)")]
    public async Task Index_InChrome_ShouldRenderTheDocumentWithoutErrors(string path, string title)
    {
        using var playwright = await Playwright.CreateAsync();
        // The installed Chrome (the GitHub runner image has one), so no browser download is needed
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "chrome" });
        var page = await browser.NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
                errors.Add(message.Text);
        };
        page.PageError += (_, error) => errors.Add(error);
        // The page links Google Fonts. Answering other origins with an empty response keeps the test off the
        // internet, and keeps a failed font download from being reported as a page error
        await page.RouteAsync(
            url => !url.StartsWith(ApiOrigin, StringComparison.Ordinal),
            route => route.FulfillAsync(new() { Status = 200, Body = "" })
        );

        await page.GotoAsync(ApiOrigin + path);
        var rendered = await WaitForHeadingAsync(page, title);

        errors.Should().BeEmpty();
        rendered.Should().BeTrue("ReDoc renders the document's title as the page heading");
    }

    private static async Task<bool> WaitForHeadingAsync(IPage page, string title)
    {
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = title, Exact = true });

        try
        {
            await heading.WaitForAsync(new() { Timeout = RenderTimeoutMilliseconds });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
