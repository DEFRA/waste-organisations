using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.Tests.Versioning;

public class ApiVersioningMiddlewareTests
{
    private const string MediaType = "application/vnd.defra.waste-organisations+json";

    private readonly ApiVersioningMiddleware _subject = new();

    [Theory]
    [InlineData("/health", "2")]
    [InlineData("/health/all", "42")]
    [InlineData("/openapi/v1.json", "abc")]
    [InlineData("/redoc", "2")]
    [InlineData("/redoc/index.html", "0")]
    public async Task WhenPathIsExempt_ShouldCallNext_WhateverVersionIsRequested(string path, string version)
    {
        var context = CreateContext(path, Accept(version));

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("2")]
    [InlineData("abc")]
    public async Task WhenNoEndpointMatched_ShouldCallNext_WhateverVersionIsRequested(string version)
    {
        var context = CreateContext("/unknown", Accept(version));

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("2")]
    [InlineData("abc")]
    public async Task WhenRoutingAnswers405_ShouldCallNext_WhateverVersionIsRequested(string version)
    {
        var context = CreateContext("/organisations", Accept(version), endpoint: RoutingsOwn405Endpoint());

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData($"{MediaType}; version=1")]
    public async Task WhenEndpointHasNoDeclaredVersions_AndVersion1Requested_ShouldCallNext(string? acceptHeader)
    {
        var context = CreateContext("/organisations/1", acceptHeader, endpoint: EndpointWithNoDeclaredVersions());

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("42")]
    public async Task WhenEndpointHasNoDeclaredVersions_AndAnotherVersionRequested_ShouldReturn406(string version)
    {
        var context = CreateContext("/organisations/1", Accept(version), endpoint: EndpointWithNoDeclaredVersions());

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeFalse();
        await AssertUnsupportedVersionProblem(context);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public async Task WhenEndpointDeclaresVersions_AndOneOfThemIsRequested_ShouldCallNext(string version)
    {
        var context = CreateContext("/test", Accept(version), endpoint: EndpointWithDeclaredVersions(1, 2));

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    // A version the endpoint does not declare
    [InlineData("3")]
    [InlineData("42")]
    // A malformed version
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("1.1")]
    public async Task WhenEndpointDeclaresVersions_AndAnUnsupportedVersionIsRequested_ShouldReturn406(string version)
    {
        var context = CreateContext("/test", Accept(version), endpoint: EndpointWithDeclaredVersions(1, 2));

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeFalse();
        await AssertUnsupportedVersionProblem(context);
    }

    private static string Accept(string version) => $"{MediaType}; version={version}";

    private async Task<bool> InvokeAndWasNextCalled(HttpContext context)
    {
        var nextCalled = false;

        await _subject.InvokeAsync(
            context,
            _ =>
            {
                nextCalled = true;

                return Task.CompletedTask;
            }
        );

        return nextCalled;
    }

    private static async Task AssertUnsupportedVersionProblem(HttpContext context)
    {
        context.Response.StatusCode.Should().Be(StatusCodes.Status406NotAcceptable);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            JsonSerializerOptions.Web,
            TestContext.Current.CancellationToken
        );

        problem.Should().NotBeNull();
        problem.Status.Should().Be(StatusCodes.Status406NotAcceptable);
        problem.Detail.Should().Be("Unsupported API version");
    }

    private static RouteEndpoint EndpointWithNoDeclaredVersions() =>
        RouteEndpoint(new EndpointMetadataCollection(), "no-declared-versions");

    private static RouteEndpoint EndpointWithDeclaredVersions(params int[] versions) =>
        RouteEndpoint(new EndpointMetadataCollection(new ApiVersionsMetadata(versions)), "declared-versions");

    private static RouteEndpoint RouteEndpoint(EndpointMetadataCollection metadata, string displayName) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse("/test"), order: 0, metadata, displayName);

    // What routing sets when the path exists but no endpoint answers the method: a plain Endpoint
    // with no metadata that writes the 405.
    private static Endpoint RoutingsOwn405Endpoint() =>
        new(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "405 HTTP Method Not Supported");

    private static DefaultHttpContext CreateContext(string path, string? acceptHeader, Endpoint? endpoint = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };

        context.Request.Path = path;

        if (acceptHeader is not null)
            context.Request.Headers.Accept = acceptHeader;

        if (endpoint is not null)
            context.SetEndpoint(endpoint);

        return context;
    }
}
