using System.Net;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteOrganisations.Api.Tests.Endpoints.Versioning;

public class VersioningTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private const string MediaType = "application/vnd.defra.waste-organisations+json";
    private static readonly Guid s_organisationId = new("b6f76437-65b6-4ed2-a7d5-c50e9af76201");

    private IOrganisationService MockOrganisationService { get; } = Substitute.For<IOrganisationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IOrganisationService>(_ => MockOrganisationService);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("*/*")]
    [InlineData("text/html")]
    [InlineData(MediaType)]
    [InlineData($"{MediaType}; version=1")]
    public async Task WhenServedAsV1_ShouldBehaveAsToday(string? acceptHeaderValue)
    {
        MockOrganisationService
            .Get(s_organisationId, Arg.Any<CancellationToken>())
            .Returns(OrganisationEntityFixtures.Default().With(x => x.Id, s_organisationId).Create());

        var response = await SendGetOrganisation(acceptHeaderValue);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task WhenVersion2Requested_ShouldBeNotAcceptable()
    {
        var response = await SendGetOrganisation($"{MediaType}; version=2");
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        // Written directly rather than through the exception handler, so the HSTS header survives (v1 X-HDR-01).
        response.Headers.GetValues("Strict-Transport-Security").Should().Equal("max-age=31536000; includeSubDomains");
        await VerifyJson(content);
    }

    [Theory]
    [InlineData($"{MediaType}; version=abc")]
    [InlineData($"{MediaType}; version=0")]
    [InlineData($"{MediaType}; version=1.1")]
    public async Task WhenVersionIsMalformed_ShouldBeNotAcceptable(string acceptHeaderValue)
    {
        var response = await SendGetOrganisation(acceptHeaderValue);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Fact]
    public async Task WhenConflictingVersionsRequested_ShouldBeNotAcceptable()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, Testing.Endpoints.Organisations.Get(s_organisationId));
        request.Headers.TryAddWithoutValidation("Accept", $"{MediaType}; version=1");
        request.Headers.TryAddWithoutValidation("Accept", $"{MediaType}; version=2");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Fact]
    public async Task WhenNoCredentials_AndVersion2Requested_ShouldBeUnauthorized_NotNotAcceptable()
    {
        var client = CreateClient(addAuthorizationHeader: false);
        var request = new HttpRequestMessage(HttpMethod.Get, Testing.Endpoints.Organisations.Get(s_organisationId));
        request.Headers.TryAddWithoutValidation("Accept", $"{MediaType}; version=2");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("health")]
    [InlineData("openapi/v1.json")]
    [InlineData("redoc/index.html")]
    public async Task WhenExemptPath_AndVersion2Requested_ShouldBeOk(string path)
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Accept", $"{MediaType}; version=2");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenUnknownPath_AndVersion2Requested_ShouldBeNotFound()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "unknown-path");
        request.Headers.TryAddWithoutValidation("Accept", $"{MediaType}; version=2");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> SendGetOrganisation(string? acceptHeaderValue)
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, Testing.Endpoints.Organisations.Get(s_organisationId));

        if (acceptHeaderValue is not null)
            request.Headers.TryAddWithoutValidation("Accept", acceptHeaderValue);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
