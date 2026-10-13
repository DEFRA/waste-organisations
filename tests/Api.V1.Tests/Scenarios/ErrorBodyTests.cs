using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// What a client gets back with each kind of error: 401, 403 and 404 carry no body at all, while a
/// 400 is always a problem details document, whether the query or the request body was rejected.
/// Clients may parse 400 bodies, so their shape is part of the contract.
/// </summary>
public class ErrorBodyTests : V1TestBase
{
    [Theory]
    [Trait("Scenario", "X-BODY-01")]
    [InlineData("UnauthorizedAnonymous")]
    [InlineData("ForbiddenReadOnlyWrite")]
    [InlineData("NotFoundUnknownOrganisation")]
    [InlineData("NotFoundNonGuidId")]
    [InlineData("NotFoundRegistrationOnUnknownOrganisation")]
    public async Task Error_WithoutValidationFailure_HasEmptyBody(string errorCase)
    {
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var anonymous = CreateClient();
        using var readOnly = CreateClient(TestClient.ReadOnlyApiKey);
        using var developer = CreateClient(TestClient.Developer);

        var (expected, send) = errorCase switch
        {
            "UnauthorizedAnonymous" => (
                HttpStatusCode.Unauthorized,
                anonymous.GetAsync("/organisations", cancellationToken)
            ),
            "ForbiddenReadOnlyWrite" => (
                HttpStatusCode.Forbidden,
                readOnly.PutAsync($"/organisations/{id}", Json(OrganisationPayloads.Full()), cancellationToken)
            ),
            "NotFoundUnknownOrganisation" => (
                HttpStatusCode.NotFound,
                developer.GetAsync($"/organisations/{id}", cancellationToken)
            ),
            "NotFoundNonGuidId" => (
                HttpStatusCode.NotFound,
                developer.GetAsync("/organisations/not-a-guid", cancellationToken)
            ),
            "NotFoundRegistrationOnUnknownOrganisation" => (
                HttpStatusCode.NotFound,
                developer.PutAsync(
                    $"/organisations/{id}/registrations/SMALL_PRODUCER-2025",
                    Json("""{ "status": "REGISTERED" }"""),
                    cancellationToken
                )
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(errorCase), errorCase, "No such case"),
        };
        using var response = await send;

        response.StatusCode.Should().Be(expected);
        (await response.Content.ReadAsStringAsync(cancellationToken)).Should().BeEmpty();
    }

    // Two sources of 400 are covered: validation of query values, which lists the failures under
    // `errors`, and a body the API cannot read, which explains the failure in `detail`. Both share
    // the problem details members a client can rely on.
    [Theory]
    [Trait("Scenario", "X-BODY-01")]
    [InlineData("InvalidQuery")]
    [InlineData("UnreadableBody")]
    public async Task BadRequest_IsProblemDetailsWithTypeTitleStatusAndTraceId(string errorCase)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var developer = CreateClient(TestClient.Developer);

        using var response = errorCase switch
        {
            "InvalidQuery" => await developer.GetAsync("/organisations?statuses=NOT_A_STATUS", cancellationToken),
            "UnreadableBody" => await developer.PutAsync(
                $"/organisations/{Guid.NewGuid()}",
                Json("""{ "name": """),
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(errorCase), errorCase, "No such case"),
        };

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
        body["type"]!.GetValue<string>().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
        body["title"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        body["status"]!.GetValue<int>().Should().Be(400);
        body["traceId"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }
}
