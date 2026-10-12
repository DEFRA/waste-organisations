using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

// Every organisation here is created with SMALL_PRODUCER-2025, so the registrations these tests
// put are always ones the organisation does not have yet unless the test says otherwise.
public class PutRegistrationTests : V1TestBase
{
    private const string Registered = """{ "status": "REGISTERED" }""";
    private const string Cancelled = """{ "status": "CANCELLED" }""";

    // The response body is the registration alone, not the whole organisation.
    [Fact]
    [Trait("Scenario", "REG-PUT-01")]
    public async Task PutRegistration_WhenOrganisationExistsAndRegistrationDoesNot_ReturnsCreatedWithLocation()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await PutRegistration(id, "EXPORTER-2030", Registered);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().Be($"/organisations/{id}/registrations/EXPORTER-2030");
        var registration = ParseRegistration(body);
        registration.Created.Should().Be(registration.Updated);
        await VerifyJson(body);
    }

    [Fact]
    [Trait("Scenario", "REG-PUT-02")]
    public async Task PutRegistration_WhenStatusChanged_ReturnsOkAdvancesUpdatedAndKeepsCreated()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        var (_, createdBody) = await PutRegistration(id, "EXPORTER-2030", Registered);
        var before = ParseRegistration(createdBody);
        await Wait();

        var (response, body) = await PutRegistration(id, "EXPORTER-2030", Cancelled);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = ParseRegistration(body);
        after.Status.Should().Be("CANCELLED");
        after.Created.Should().Be(before.Created);
        after.Updated.Should().BeAfter(before.Updated);
        (await GetRegistrations(id)).Should().ContainEquivalentOf(after);
    }

    // A repeat with the same status is a no-op, so the timestamps keep the values from the
    // first request even though time has moved on.
    [Fact]
    [Trait("Scenario", "REG-PUT-03")]
    public async Task PutRegistration_WhenSameStatus_ReturnsOkWithTimestampsUnchanged()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        var (_, createdBody) = await PutRegistration(id, "EXPORTER-2030", Registered);
        var before = ParseRegistration(createdBody);
        await Wait();

        var (response, body) = await PutRegistration(id, "EXPORTER-2030", Registered);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ParseRegistration(body).Should().Be(before);
        (await GetRegistrations(id)).Should().ContainEquivalentOf(before);
    }

    [Fact]
    [Trait("Scenario", "REG-PUT-04")]
    public async Task PutRegistration_WhenOrganisationUnknown_ReturnsNotFoundWithEmptyBody()
    {
        var id = Guid.NewGuid();

        var (response, body) = await PutRegistration(id, "EXPORTER-2030", Registered);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
        using var client = CreateClient(TestClient.Developer);
        using var get = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 2023 and 2050 are the inclusive bounds of the accepted registration years.
    [Theory]
    [Trait("Scenario", "REG-PUT-05")]
    [InlineData("SMALL_PRODUCER", 2023)]
    [InlineData("SMALL_PRODUCER", 2050)]
    [InlineData("LARGE_PRODUCER", 2023)]
    [InlineData("LARGE_PRODUCER", 2050)]
    [InlineData("COMPLIANCE_SCHEME", 2023)]
    [InlineData("COMPLIANCE_SCHEME", 2050)]
    [InlineData("REPROCESSOR", 2023)]
    [InlineData("REPROCESSOR", 2050)]
    [InlineData("EXPORTER", 2023)]
    [InlineData("EXPORTER", 2050)]
    public async Task PutRegistration_WhenEachTypeAtBoundaryYear_ReturnsCreated(string type, int year)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await PutRegistration(id, $"{type}-{year}", Registered);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var registration = ParseRegistration(body);
        registration.Type.Should().Be(type);
        registration.RegistrationYear.Should().Be(year);
        registration.Status.Should().Be("REGISTERED");
    }

    // Adding a registration and changing the status of an existing one must both leave the
    // organisation's own fields and every other registration exactly as they were.
    [Theory]
    [Trait("Scenario", "REG-PUT-08")]
    [InlineData("EXPORTER-2030", "REGISTERED")]
    [InlineData("SMALL_PRODUCER-2025", "CANCELLED")]
    public async Task PutRegistration_LeavesOtherRegistrationsAndOrganisationFieldsUntouched(string key, string status)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using (var client = CreateClient(TestClient.Developer))
        {
            using var second = await client.PutAsync(
                $"/organisations/{id}",
                Json(OrganisationPayloads.Full(type: "LARGE_PRODUCER", registrationYear: 2026)),
                TestContext.Current.CancellationToken
            );
            second.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var before = JsonNode.Parse(await GetOrganisation(id))!.AsObject();
        await Wait();

        var (response, _) = await PutRegistration(id, key, $$"""{ "status": "{{status}}" }""");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var after = JsonNode.Parse(await GetOrganisation(id))!.AsObject();
        foreach (var (name, value) in before.Where(p => p.Key != "registrations"))
            JsonNode.DeepEquals(after[name], value).Should().BeTrue($"\"{name}\" must be unchanged");
        Others(after).Should().BeEquivalentTo(Others(before));

        IEnumerable<string> Others(JsonObject organisation) =>
            organisation["registrations"]!
                .AsArray()
                .Where(r => $"{r!["type"]}-{r["registrationYear"]}" != key)
                .Select(r => r!.ToJsonString());
    }

    private static async Task<(HttpResponseMessage Response, string Body)> PutRegistration(
        Guid id,
        string key,
        string body
    )
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.PutAsync(
            $"/organisations/{id}/registrations/{key}",
            Json(body),
            TestContext.Current.CancellationToken
        );

        return (response, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<string> GetOrganisation(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<Registration>> GetRegistrations(Guid id)
    {
        using var document = JsonDocument.Parse(await GetOrganisation(id));

        return document.RootElement.GetProperty("registrations").EnumerateArray().Select(ToRegistration).ToList();
    }

    private static Registration ParseRegistration(string registrationBody)
    {
        using var document = JsonDocument.Parse(registrationBody);

        return ToRegistration(document.RootElement);
    }

    private static Registration ToRegistration(JsonElement r) =>
        new(
            r.GetProperty("type").GetString()!,
            r.GetProperty("registrationYear").GetInt32(),
            r.GetProperty("status").GetString()!,
            r.GetProperty("created").GetDateTimeOffset(),
            r.GetProperty("updated").GetDateTimeOffset()
        );

    private sealed record Registration(
        string Type,
        int RegistrationYear,
        string Status,
        DateTimeOffset Created,
        DateTimeOffset Updated
    );
}
