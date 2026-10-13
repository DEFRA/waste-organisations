using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// How authentication interacts with the rest of the request: it is checked before any input is
/// read, and a Basic value the API cannot decode is not treated as a failed login.
/// </summary>
public class AuthEdgeCaseTests : V1TestBase
{
    /// <summary>
    /// One invalid request per endpoint, as method, path and body (<c>null</c> for no body).
    /// The path takes the organisation id as <c>{id}</c>.
    /// </summary>
    private static (HttpMethod Method, string Path, string? Body) InvalidRequest(string requestCase) =>
        requestCase switch
        {
            "PutOrganisationMalformedJson" => (HttpMethod.Put, "/organisations/{id}", """{ "name": """),
            "PutOrganisationInvalidValues" => (
                HttpMethod.Put,
                "/organisations/{id}",
                """{ "address": {}, "registration": { "status": "NOT_A_STATUS", "type": "NOT_A_TYPE", "registrationYear": 2022 } }"""
            ),
            "PutRegistrationInvalid" => (
                HttpMethod.Put,
                "/organisations/{id}/registrations/NOT_A_TYPE-2022",
                """{ "status": "NOT_A_STATUS" }"""
            ),
            "DeleteRegistrationInvalidKey" => (
                HttpMethod.Delete,
                "/organisations/{id}/registrations/NOT_A_TYPE-2022",
                null
            ),
            "SearchInvalidFilter" => (HttpMethod.Get, "/organisations?registrations=NOT_A_TYPE", null),
            _ => throw new ArgumentOutOfRangeException(nameof(requestCase), requestCase, "No such case"),
        };

    // The same request is sent anonymously and then as a full-scope client, so the test proves the
    // input really is invalid (400) and that the anonymous caller is told 401 without seeing that.
    [Theory]
    [Trait("Scenario", "X-AUTH-02")]
    [InlineData("PutOrganisationMalformedJson")]
    [InlineData("PutOrganisationInvalidValues")]
    [InlineData("PutRegistrationInvalid")]
    [InlineData("DeleteRegistrationInvalidKey")]
    [InlineData("SearchInvalidFilter")]
    public async Task InvalidRequest_WithoutCredentials_ReturnsUnauthorizedNotBadRequest(string requestCase)
    {
        var id = Guid.NewGuid();
        var (method, path, body) = InvalidRequest(requestCase);
        using var anonymous = CreateClient();
        using var developer = CreateClient(TestClient.Developer);

        using var anonymousResponse = await Send(anonymous, method, path.Replace("{id}", id.ToString()), body);
        using var developerResponse = await Send(developer, method, path.Replace("{id}", id.ToString()), body);

        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        developerResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var get = await developer.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Characterisation: documents current behaviour, not a requirement
    // The Basic handler decodes the value without guarding against bad base64, so the exception
    // escapes as a 500 instead of a 401. Likely bug. Every endpoint is affected, since every
    // endpoint runs the same handler; write requests are sent against a stored organisation and
    // must leave it as it was.
    [Theory]
    [Trait("Scenario", "X-AUTH-03")]
    [InlineData("SearchOrganisations")]
    [InlineData("GetOrganisation")]
    [InlineData("PutOrganisation")]
    [InlineData("PutRegistration")]
    [InlineData("DeleteRegistration")]
    public async Task Request_WithNonBase64BasicValue_ReturnsInternalServerError(string endpoint)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var developer = CreateClient(TestClient.Developer);
        var before = await GetBody(developer, id);
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "not-base64!!");

        using var response = endpoint switch
        {
            "SearchOrganisations" => await Send(client, HttpMethod.Get, "/organisations", null),
            "GetOrganisation" => await Send(client, HttpMethod.Get, $"/organisations/{id}", null),
            "PutOrganisation" => await Send(
                client,
                HttpMethod.Put,
                $"/organisations/{id}",
                OrganisationPayloads.Full(status: "CANCELLED")
            ),
            "PutRegistration" => await Send(
                client,
                HttpMethod.Put,
                $"/organisations/{id}/registrations/SMALL_PRODUCER-2025",
                """{ "status": "CANCELLED" }"""
            ),
            "DeleteRegistration" => await Send(
                client,
                HttpMethod.Delete,
                $"/organisations/{id}/registrations/SMALL_PRODUCER-2025",
                null
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "No such endpoint"),
        };

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await GetBody(developer, id)).Should().Be(before);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = Json(body);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string> GetBody(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
