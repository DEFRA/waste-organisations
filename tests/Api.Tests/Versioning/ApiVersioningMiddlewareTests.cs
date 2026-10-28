using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.Tests.Versioning;

public class ApiVersioningMiddlewareTests
{
    private readonly ApiVersioningMiddleware _subject = new();

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/all")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/redoc")]
    [InlineData("/redoc/index.html")]
    public async Task WhenPathIsExempt_ShouldCallNext_RegardlessOfVersion(string path)
    {
        var context = CreateContext(path, acceptHeader: "application/vnd.defra.waste-organisations+json; version=2");

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task WhenNoEndpointMatched_ShouldCallNext()
    {
        var context = CreateContext(
            "/unknown",
            acceptHeader: "application/vnd.defra.waste-organisations+json; version=2"
        );

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task WhenRoutingAnswers405_ShouldCallNext_WhateverVersionIsRequested()
    {
        var context = CreateContext(
            "/organisations",
            acceptHeader: "application/vnd.defra.waste-organisations+json; version=2",
            endpoint: RoutingsOwn405Endpoint()
        );

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("application/vnd.defra.waste-organisations+json; version=1")]
    public async Task WhenEndpointHasNoDeclaredVersions_AndVersion1Requested_ShouldCallNext(string? acceptHeader)
    {
        var context = CreateContext("/organisations/1", acceptHeader, endpoint: EndpointWithNoDeclaredVersions());

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task WhenEndpointHasNoDeclaredVersions_AndVersion2Requested_ShouldReturn406()
    {
        var context = CreateContext(
            "/organisations/1",
            "application/vnd.defra.waste-organisations+json; version=2",
            endpoint: EndpointWithNoDeclaredVersions()
        );

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeFalse();
        await AssertUnsupportedVersionProblem(context);
    }

    [Fact]
    public async Task WhenEndpointDeclaresVersion2_AndVersion2Requested_ShouldCallNext()
    {
        var context = CreateContext(
            "/test",
            "application/vnd.defra.waste-organisations+json; version=2",
            endpoint: EndpointWithDeclaredVersions(1, 2)
        );

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task WhenEndpointDeclaresVersions1And2_AndVersion3Requested_ShouldReturn406()
    {
        var context = CreateContext(
            "/test",
            "application/vnd.defra.waste-organisations+json; version=3",
            endpoint: EndpointWithDeclaredVersions(1, 2)
        );

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeFalse();
        await AssertUnsupportedVersionProblem(context);
    }

    [Fact]
    public async Task WhenAcceptHeaderIsMalformed_ShouldReturn406_EvenWhenEndpointDeclaresThatVersion()
    {
        var context = CreateContext(
            "/test",
            "application/vnd.defra.waste-organisations+json; version=abc",
            endpoint: EndpointWithDeclaredVersions(1, 2)
        );

        var nextCalled = await InvokeAndWasNextCalled(context);

        nextCalled.Should().BeFalse();
        await AssertUnsupportedVersionProblem(context);
    }

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
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("Unsupported API version");
    }

    private static Endpoint EndpointWithNoDeclaredVersions() =>
        RouteEndpoint(new EndpointMetadataCollection(), "no-declared-versions");

    private static Endpoint EndpointWithDeclaredVersions(params int[] versions) =>
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
