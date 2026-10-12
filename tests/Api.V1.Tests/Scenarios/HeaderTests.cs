using System.Net;
using System.Text;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// The HSTS header tells browsers to use HTTPS only. The API adds it to every response it builds
/// itself, whatever the status or authentication, but responses written by the exception handler
/// lose it.
/// </summary>
public class HeaderTests : V1TestBase
{
    private const string Hsts = "Strict-Transport-Security";

    private const string ExpectedHsts = "max-age=31536000; includeSubDomains";

    [Theory]
    [Trait("Scenario", "X-HDR-01")]
    [InlineData("HealthOk")]
    [InlineData("SearchOk")]
    [InlineData("GetOk")]
    [InlineData("CreatedOrganisation")]
    [InlineData("DeletedRegistration")]
    [InlineData("BadRequestFromQuery")]
    [InlineData("UnauthorizedAnonymous")]
    [InlineData("ForbiddenReadOnlyWrite")]
    [InlineData("NotFoundUnknownOrganisation")]
    [InlineData("NotFoundUnknownPath")]
    [InlineData("MethodNotAllowed")]
    [InlineData("UnsupportedMediaType")]
    public async Task Response_HasHstsHeader(string responseCase)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var anonymous = CreateClient();
        using var readOnly = CreateClient(TestClient.ReadOnlyApiKey);
        using var developer = CreateClient(TestClient.Developer);

        var (expected, send) = responseCase switch
        {
            "HealthOk" => (HttpStatusCode.OK, anonymous.GetAsync("/health", cancellationToken)),
            "SearchOk" => (HttpStatusCode.OK, developer.GetAsync("/organisations", cancellationToken)),
            "GetOk" => (HttpStatusCode.OK, developer.GetAsync($"/organisations/{id}", cancellationToken)),
            "CreatedOrganisation" => (
                HttpStatusCode.Created,
                developer.PutAsync(
                    $"/organisations/{Guid.NewGuid()}",
                    Json(OrganisationPayloads.Full()),
                    cancellationToken
                )
            ),
            "DeletedRegistration" => (
                HttpStatusCode.NoContent,
                developer.DeleteAsync($"/organisations/{id}/registrations/SMALL_PRODUCER-2025", cancellationToken)
            ),
            "BadRequestFromQuery" => (
                HttpStatusCode.BadRequest,
                developer.GetAsync("/organisations?statuses=NOT_A_STATUS", cancellationToken)
            ),
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
                developer.GetAsync($"/organisations/{Guid.NewGuid()}", cancellationToken)
            ),
            "NotFoundUnknownPath" => (HttpStatusCode.NotFound, developer.GetAsync("/no-such-path", cancellationToken)),
            "MethodNotAllowed" => (
                HttpStatusCode.MethodNotAllowed,
                developer.DeleteAsync($"/organisations/{id}", cancellationToken)
            ),
            "UnsupportedMediaType" => (
                HttpStatusCode.UnsupportedMediaType,
                developer.PutAsync(
                    $"/organisations/{id}",
                    new StringContent(OrganisationPayloads.Full(), Encoding.UTF8, "text/plain"),
                    cancellationToken
                )
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(responseCase), responseCase, "No such case"),
        };
        using var response = await send;

        response.StatusCode.Should().Be(expected);
        response.Headers.GetValues(Hsts).Should().Equal(ExpectedHsts);
    }

    // Characterisation: documents current behaviour, not a requirement
    // A body the API cannot bind and a Basic value it cannot decode both throw, and the exception
    // handler clears the response headers before writing its problem details, HSTS included.
    // Likely bug.
    [Theory]
    [Trait("Scenario", "X-HDR-01")]
    [InlineData("UnreadableBody")]
    [InlineData("RequiredFieldMissing")]
    [InlineData("NonBase64Basic")]
    public async Task ResponseFromExceptionHandler_HasNoHstsHeader(string responseCase)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var developer = CreateClient(TestClient.Developer);
        using var nonBase64 = CreateClient();
        nonBase64.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Basic not-base64!!");

        var (expected, send) = responseCase switch
        {
            "UnreadableBody" => (
                HttpStatusCode.BadRequest,
                developer.PutAsync($"/organisations/{Guid.NewGuid()}", Json("""{ "name": """), cancellationToken)
            ),
            "RequiredFieldMissing" => (
                HttpStatusCode.BadRequest,
                developer.PutAsync($"/organisations/{Guid.NewGuid()}", Json("{}"), cancellationToken)
            ),
            "NonBase64Basic" => (
                HttpStatusCode.InternalServerError,
                nonBase64.GetAsync("/organisations", cancellationToken)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(responseCase), responseCase, "No such case"),
        };
        using var response = await send;

        response.StatusCode.Should().Be(expected);
        response.Headers.Contains(Hsts).Should().BeFalse();
    }
}
