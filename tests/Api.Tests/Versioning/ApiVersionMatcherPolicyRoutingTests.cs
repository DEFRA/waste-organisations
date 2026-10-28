using System.Net;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Defra.WasteOrganisations.Api.Tests.Versioning;

/// <summary>
/// Routes requests through the real matcher with <see cref="ApiVersionMatcherPolicy"/> and
/// <see cref="ApiVersioningMiddleware"/>, on a small app whose endpoints cover every shape the v2
/// stories need: one path with a handler per version, one handler serving two versions, a path
/// where v2 adds a method v1 lacks, a v2-only path, and a path that declares nothing.
/// </summary>
public sealed class ApiVersionMatcherPolicyRoutingTests : IAsyncLifetime
{
    private const string MediaType = "application/vnd.defra.waste-organisations+json";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails();
        builder.Services.AddApiVersioning();

        _app = builder.Build();
        _app.UseApiVersioning();

        _app.MapGet("/things/{id:int}", (int id) => $"get v1 {id}");
        _app.MapGet("/things/{id:int}", (int id) => $"get v2 {id}").WithApiVersions(2);
        _app.MapPut("/things/{id:int}", (int id) => $"put v1 and v2 {id}").WithApiVersions(1, 2);
        _app.MapGet("/things", () => "search v1");
        _app.MapPost("/things", () => "create v2").WithApiVersions(2);
        _app.MapGet("/v2-only", () => "v2 only").WithApiVersions(2);
        _app.MapGet("/plain", () => "plain v1");

        await _app.StartAsync(TestContext.Current.CancellationToken);
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Theory]
    // One path, one handler per version
    [InlineData("GET", "/things/7", null, 200, "get v1 7")]
    [InlineData("GET", "/things/7", "1", 200, "get v1 7")]
    [InlineData("GET", "/things/7", "2", 200, "get v2 7")]
    [InlineData("GET", "/things/7", "3", 406, null)]
    [InlineData("GET", "/things/7", "abc", 406, null)]
    // One handler serving two versions
    [InlineData("PUT", "/things/7", null, 200, "put v1 and v2 7")]
    [InlineData("PUT", "/things/7", "2", 200, "put v1 and v2 7")]
    [InlineData("PUT", "/things/7", "3", 406, null)]
    // v2 adds a method v1 lacks: v1 keeps its 405, v2 gets the handler, a v1-only method stays 406 for v2
    [InlineData("GET", "/things", null, 200, "search v1")]
    [InlineData("GET", "/things", "2", 406, null)]
    [InlineData("POST", "/things", null, 405, null)]
    [InlineData("POST", "/things", "2", 200, "create v2")]
    [InlineData("POST", "/things", "3", 406, null)]
    // A v2-only path does not exist for v1
    [InlineData("GET", "/v2-only", null, 404, null)]
    [InlineData("GET", "/v2-only", "2", 200, "v2 only")]
    [InlineData("GET", "/v2-only", "3", 406, null)]
    // Nothing declared: unchanged behaviour. No endpoint at all: 404 whatever the version
    [InlineData("GET", "/plain", null, 200, "plain v1")]
    [InlineData("GET", "/plain", "2", 406, null)]
    [InlineData("GET", "/missing", "2", 404, null)]
    public async Task Request_IsRoutedToTheHandlerForItsVersion(
        string method,
        string path,
        string? version,
        int expectedStatus,
        string? expectedBody
    )
    {
        using var response = await Send(method, path, version);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        ((int)response.StatusCode).Should().Be(expectedStatus);

        if (expectedBody is not null)
            body.Should().Be(expectedBody);

        if (expectedStatus == StatusCodes.Status406NotAcceptable)
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // A method no version answers is 405 whatever the version, listing the requested version's methods.
    [Theory]
    [InlineData("POST", "/things", null, new[] { "GET" })]
    [InlineData("DELETE", "/things/7", null, new[] { "GET", "PUT" })]
    [InlineData("DELETE", "/things/7", "2", new[] { "GET", "PUT" })]
    [InlineData("DELETE", "/things/7", "3", new[] { "GET", "PUT" })]
    public async Task UnsupportedMethod_ReturnsMethodNotAllowed_ListingTheVersionsMethods(
        string method,
        string path,
        string? version,
        string[] allowed
    )
    {
        using var response = await Send(method, path, version);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.Allow.Should().BeEquivalentTo(allowed);
    }

    private async Task<HttpResponseMessage> Send(string method, string path, string? version)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (version is not null)
            request.Headers.TryAddWithoutValidation("Accept", $"{MediaType}; version={version}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
