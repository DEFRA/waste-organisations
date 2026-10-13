using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

// Characterisation: documents current behaviour, not a requirement
// ORG-SEARCH-15 pins how the search filters treat input that is neither clearly valid nor
// clearly invalid. Values are matched case-sensitively and split on every comma, so an empty
// item fails; each item is then parsed leniently, so surrounding spaces and enum numbers pass.
public class SearchOrganisationsEdgeCaseTests : V1TestBase
{
    // An empty value, or the empty item after a trailing comma, is listed as an invalid value of "".
    [Theory]
    [Trait("Scenario", "ORG-SEARCH-15")]
    [InlineData("lowerCaseType")]
    [InlineData("lowerCaseStatus")]
    [InlineData("emptyRegistrations")]
    [InlineData("emptyRegistrationYears")]
    [InlineData("emptyStatuses")]
    [InlineData("trailingComma")]
    public async Task Search_WhenValueIsLowerCaseOrEmpty_ReturnsBadRequest(string queryCase)
    {
        var query = queryCase switch
        {
            "lowerCaseType" => "registrations=small_producer",
            "lowerCaseStatus" => "statuses=registered",
            "emptyRegistrations" => "registrations=",
            "emptyRegistrationYears" => "registrationYears=",
            "emptyStatuses" => "statuses=",
            "trailingComma" => "registrations=SMALL_PRODUCER,",
            _ => throw new ArgumentOutOfRangeException(nameof(queryCase), queryCase, "No such case"),
        };
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .UseParameters(queryCase);
    }

    // Three organisations, each with one 2025 registration: SMALL_PRODUCER and EXPORTER registered,
    // LARGE_PRODUCER cancelled, so a filter's effect shows in which of them come back.
    // Numbers are read as the enum's position: type 1 is LARGE_PRODUCER and status 1 is CANCELLED.
    // Likely bug: the documented contract is the string name only, and a number with no type
    // (99) is accepted and matches nothing instead of failing with 400.
    [Theory]
    [Trait("Scenario", "ORG-SEARCH-15")]
    [InlineData("registrations=SMALL_PRODUCER,%20EXPORTER", "SMALL_PRODUCER", "EXPORTER")]
    [InlineData("registrationYears=%202025", "SMALL_PRODUCER", "LARGE_PRODUCER", "EXPORTER")]
    [InlineData("registrations=SMALL_PRODUCER,SMALL_PRODUCER", "SMALL_PRODUCER")]
    [InlineData("registrations=SMALL_PRODUCER&registrations=EXPORTER", "SMALL_PRODUCER", "EXPORTER")]
    [InlineData("registrations=EXPORTER&notAFilter=NOT_A_TYPE", "EXPORTER")]
    [InlineData("registrations=1", "LARGE_PRODUCER")]
    [InlineData("statuses=1", "LARGE_PRODUCER")]
    [InlineData("registrations=99")]
    public async Task Search_WhenValueIsLenientlyParsed_ReturnsOkWithMatchingOrganisations(
        string query,
        params string[] expected
    )
    {
        var idsByType = new Dictionary<string, Guid>
        {
            ["SMALL_PRODUCER"] = await CreateOrganisation(OrganisationPayloads.Full(type: "SMALL_PRODUCER")),
            ["LARGE_PRODUCER"] = await CreateOrganisation(
                OrganisationPayloads.Full(type: "LARGE_PRODUCER", status: "CANCELLED")
            ),
            ["EXPORTER"] = await CreateOrganisation(OrganisationPayloads.Full(type: "EXPORTER")),
        };
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var returnedIds = body["organisations"]!.AsArray().Select(o => o!["id"]!.GetValue<Guid>()).ToHashSet();
        idsByType.Where(p => returnedIds.Contains(p.Value)).Select(p => p.Key).Should().BeEquivalentTo(expected);
    }
}
