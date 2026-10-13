using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

// Each filter is checked as a whole comma-separated string, and the 400 lists only the values
// that failed, joined by ", ", under the parameter's property name.
public class SearchOrganisationsValidationTests : V1TestBase
{
    [Theory]
    [Trait("Scenario", "ORG-SEARCH-11")]
    [InlineData("unknown")]
    [InlineData("validAndUnknown")]
    public async Task Search_WhenRegistrationTypeInvalid_ReturnsBadRequestListingInvalidValues(string queryCase)
    {
        var query = queryCase switch
        {
            "unknown" => "registrations=NOT_A_TYPE",
            "validAndUnknown" => "registrations=SMALL_PRODUCER,NOT_A_TYPE,ALSO_NOT_A_TYPE",
            _ => throw new ArgumentOutOfRangeException(nameof(queryCase), queryCase, "No such case"),
        };

        var (status, body) = await Search(query);

        status.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(body).UseParameters(queryCase);
    }

    // 2023 and 2050 are the first and last years accepted, so 2022 and 2051 are the nearest invalid ones.
    [Theory]
    [Trait("Scenario", "ORG-SEARCH-12")]
    [InlineData("notANumber")]
    [InlineData("year2022")]
    [InlineData("year2051")]
    [InlineData("validAndInvalid")]
    public async Task Search_WhenRegistrationYearInvalid_ReturnsBadRequestListingInvalidValues(string queryCase)
    {
        var query = queryCase switch
        {
            "notANumber" => "registrationYears=abc",
            "year2022" => "registrationYears=2022",
            "year2051" => "registrationYears=2051",
            "validAndInvalid" => "registrationYears=2023,2022,abc,2051",
            _ => throw new ArgumentOutOfRangeException(nameof(queryCase), queryCase, "No such case"),
        };

        var (status, body) = await Search(query);

        status.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(body).UseParameters(queryCase);
    }

    [Theory]
    [Trait("Scenario", "ORG-SEARCH-13")]
    [InlineData("unknown")]
    [InlineData("validAndUnknown")]
    public async Task Search_WhenStatusInvalid_ReturnsBadRequestListingInvalidValues(string queryCase)
    {
        var query = queryCase switch
        {
            "unknown" => "statuses=PENDING",
            "validAndUnknown" => "statuses=REGISTERED,NOT_A_STATUS",
            _ => throw new ArgumentOutOfRangeException(nameof(queryCase), queryCase, "No such case"),
        };

        var (status, body) = await Search(query);

        status.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(body).UseParameters(queryCase);
    }

    // Validation does not stop at the first invalid parameter: one 400 carries an error for each.
    [Fact]
    [Trait("Scenario", "ORG-SEARCH-14")]
    public async Task Search_WhenSeveralParametersInvalid_ReturnsOneErrorPerParameter()
    {
        var (status, body) = await Search("registrations=NOT_A_TYPE&registrationYears=2022&statuses=NOT_A_STATUS");

        status.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(body);
    }

    private static async Task<(HttpStatusCode Status, string Body)> Search(string query)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations?{query}", TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
