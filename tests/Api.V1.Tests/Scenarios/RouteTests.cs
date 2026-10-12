using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// What a client gets for a path the API knows with a method it does not support, and for a path
/// it does not know. Routing runs before authentication, so the answer is the same with or
/// without credentials.
/// </summary>
public class RouteTests : V1TestBase
{
    [Theory]
    [Trait("Scenario", "X-ROUTE-01")]
    [InlineData("POST", "/organisations", new[] { "GET" }, false)]
    [InlineData("POST", "/organisations", new[] { "GET" }, true)]
    [InlineData("DELETE", "/organisations/{id}", new[] { "GET", "PUT" }, false)]
    [InlineData("DELETE", "/organisations/{id}", new[] { "GET", "PUT" }, true)]
    [InlineData("GET", "/organisations/{id}/registrations/SMALL_PRODUCER-2025", new[] { "DELETE", "PUT" }, false)]
    [InlineData("GET", "/organisations/{id}/registrations/SMALL_PRODUCER-2025", new[] { "DELETE", "PUT" }, true)]
    public async Task UnsupportedMethod_OnExistingPath_ReturnsMethodNotAllowedWithAllowHeader(
        string method,
        string path,
        string[] allowed,
        bool withCredentials
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var client = withCredentials ? CreateClient(TestClient.Developer) : CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", id.ToString()));

        using var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.Allow.Should().Equal(allowed);
        (await response.Content.ReadAsStringAsync(cancellationToken)).Should().BeEmpty();
    }

    // The organisation exists and has a registration, so the 404 can only mean that no endpoint
    // lists registrations: a client reads them from the organisation instead.
    [Fact]
    [Trait("Scenario", "X-ROUTE-02")]
    public async Task GetRegistrations_OfExistingOrganisation_ReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}/registrations", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(cancellationToken)).Should().BeEmpty();
    }
}
