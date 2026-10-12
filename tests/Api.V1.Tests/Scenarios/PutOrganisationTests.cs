using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class PutOrganisationTests : V1TestBase
{
    [Fact]
    [Trait("Scenario", "ORG-PUT-01")]
    public async Task PutOrganisation_WhenNewId_ReturnsCreatedWithLocationAndOneRegistration()
    {
        var id = Guid.NewGuid();

        var (response, body) = await PutOrganisation(id, OrganisationPayloads.Full());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().Be($"/organisations/{id}");
        var registration = Registrations(body).Should().ContainSingle().Subject;
        registration.Created.Should().Be(registration.Updated);
        await VerifyJson(body);
    }

    // A PUT replaces the organisation's fields wholesale: anything optional the caller leaves out
    // is cleared, not kept from the earlier version.
    [Fact]
    [Trait("Scenario", "ORG-PUT-02")]
    public async Task PutOrganisation_WhenExistingIdWithChangedFields_ReplacesFieldsAndNullsOmittedOnes()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        const string changed = """
            {
              "name": "V1 Renamed Organisation Ltd",
              "businessCountry": "GB-WLS",
              "address": {
                "addressLine1": "2 Changed Road",
                "postcode": "CH2 2NG"
              },
              "registration": {
                "status": "REGISTERED",
                "type": "SMALL_PRODUCER",
                "registrationYear": 2025
              }
            }
            """;

        var (response, _) = await PutOrganisation(id, changed);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await GetOrganisation(id));
    }

    [Theory]
    [Trait("Scenario", "ORG-PUT-03")]
    [InlineData("LARGE_PRODUCER", 2025)]
    [InlineData("SMALL_PRODUCER", 2026)]
    public async Task PutOrganisation_WhenRegistrationHasNewTypeOrYear_AddsItAndKeepsOthers(string type, int year)
    {
        var id = Guid.NewGuid();
        var (_, createdBody) = await PutOrganisation(id, OrganisationPayloads.Full());
        var original = Registrations(createdBody).Single();

        var (response, body) = await PutOrganisation(id, OrganisationPayloads.Full(type: type, registrationYear: year));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var registrations = Registrations(body);
        registrations.Should().HaveCount(2);
        registrations.Should().ContainEquivalentOf(original);
        registrations.Should().ContainSingle(r => r.Type == type && r.RegistrationYear == year);
    }

    // An identical repeat must be a no-op for the registration, so its timestamps keep the
    // values from the first request even though time has moved on.
    [Fact]
    [Trait("Scenario", "ORG-PUT-04")]
    public async Task PutOrganisation_WhenSameRequestTwice_ReturnsCreatedThenOkWithTimestampsUnchanged()
    {
        var id = Guid.NewGuid();
        var (first, firstBody) = await PutOrganisation(id, OrganisationPayloads.Full());
        await Wait();

        var (second, secondBody) = await PutOrganisation(id, OrganisationPayloads.Full());

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        Registrations(secondBody).Should().BeEquivalentTo(Registrations(firstBody));
    }

    [Fact]
    [Trait("Scenario", "ORG-PUT-05")]
    public async Task PutOrganisation_WhenSameRegistrationWithNewStatus_AdvancesUpdatedAndKeepsCreated()
    {
        var id = Guid.NewGuid();
        var (_, createdBody) = await PutOrganisation(id, OrganisationPayloads.Full(status: "REGISTERED"));
        var before = Registrations(createdBody).Single();
        await Wait();

        var (response, body) = await PutOrganisation(id, OrganisationPayloads.Full(status: "CANCELLED"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = Registrations(body).Should().ContainSingle().Subject;
        after.Status.Should().Be("CANCELLED");
        after.Created.Should().Be(before.Created);
        after.Updated.Should().BeAfter(before.Updated);
    }

    // 2023 and 2050 are the inclusive bounds of the accepted registration years.
    [Theory]
    [Trait("Scenario", "ORG-PUT-06")]
    [InlineData(2023)]
    [InlineData(2050)]
    public async Task PutOrganisation_WhenYearAtBoundary_ReturnsCreated(int year)
    {
        var (response, body) = await PutOrganisation(Guid.NewGuid(), OrganisationPayloads.Full(registrationYear: year));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Registrations(body).Should().ContainSingle().Which.RegistrationYear.Should().Be(year);
    }

    private static async Task<(HttpResponseMessage Response, string Body)> PutOrganisation(Guid id, string body)
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.PutAsync($"/organisations/{id}", Json(body), TestContext.Current.CancellationToken);

        return (response, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<string> GetOrganisation(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static List<Registration> Registrations(string organisationBody)
    {
        using var document = JsonDocument.Parse(organisationBody);

        return document
            .RootElement.GetProperty("registrations")
            .EnumerateArray()
            .Select(r => new Registration(
                r.GetProperty("type").GetString()!,
                r.GetProperty("registrationYear").GetInt32(),
                r.GetProperty("status").GetString()!,
                r.GetProperty("created").GetDateTimeOffset(),
                r.GetProperty("updated").GetDateTimeOffset()
            ))
            .ToList();
    }

    private sealed record Registration(
        string Type,
        int RegistrationYear,
        string Status,
        DateTimeOffset Created,
        DateTimeOffset Updated
    );
}
