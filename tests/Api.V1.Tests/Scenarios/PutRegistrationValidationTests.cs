using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

// Every organisation here is created with SMALL_PRODUCER-2025 only, so a rejected request is
// shown to have stored nothing when that is still its one registration.
public class PutRegistrationValidationTests : V1TestBase
{
    private const string Registered = """{ "status": "REGISTERED" }""";
    private const string OriginalKey = "SMALL_PRODUCER-2025";

    // An unknown type fails while the route value is bound, so the 400 is a `detail`; a year
    // outside the inclusive 2023 to 2050 range fails validation, so it is an `errors` dictionary.
    [Theory]
    [Trait("Scenario", "REG-PUT-06")]
    [InlineData("NOT_A_TYPE-2025")]
    [InlineData("EXPORTER-2022")]
    [InlineData("EXPORTER-2051")]
    public async Task PutRegistration_WhenTypeOrYearInvalid_ReturnsBadRequestAndStoresNothing(string key)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await PutRegistration(id, key, Registered);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RegistrationKeys(id)).Should().Equal(OriginalKey);
        await VerifyJson(body).UseParameters(key);
    }

    // Characterisation: documents current behaviour, not a requirement
    // The route only matches an integer year, so a non-numeric year finds no endpoint at all.
    [Fact]
    [Trait("Scenario", "REG-PUT-07")]
    public async Task PutRegistration_WhenYearIsNotANumber_ReturnsNotFoundWithEmptyBody()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await PutRegistration(id, "EXPORTER-abc", Registered);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
        (await RegistrationKeys(id)).Should().Equal(OriginalKey);
    }

    // Characterisation: documents current behaviour, not a requirement
    // Type names in the route are case-sensitive, unlike JSON property names in the body.
    [Fact]
    [Trait("Scenario", "REG-PUT-07")]
    public async Task PutRegistration_WhenTypeIsLowerCase_ReturnsBadRequestAndStoresNothing()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await PutRegistration(id, "exporter-2025", Registered);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RegistrationKeys(id)).Should().Equal(OriginalKey);
        await VerifyJson(body);
    }

    // Characterisation: documents current behaviour, not a requirement
    // Each body fails before the handler runs, so the 400 `detail` comes from body binding.
    [Theory]
    [Trait("Scenario", "REG-PUT-07")]
    [InlineData("emptyObject")]
    [InlineData("invalidStatus")]
    [InlineData("lowerCaseStatus")]
    [InlineData("emptyBody")]
    public async Task PutRegistration_WhenBodyInvalid_ReturnsBadRequestAndStoresNothing(string bodyCase)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        var payload = bodyCase switch
        {
            "emptyObject" => "{}",
            "invalidStatus" => """{ "status": "NOT_A_STATUS" }""",
            "lowerCaseStatus" => """{ "status": "registered" }""",
            "emptyBody" => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(bodyCase), bodyCase, "No such case"),
        };

        var (response, body) = await PutRegistration(id, "EXPORTER-2030", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RegistrationKeys(id)).Should().Equal(OriginalKey);
        await VerifyJson(body).UseParameters(bodyCase);
    }

    // Characterisation: documents current behaviour, not a requirement
    // The route alone says which registration is written; a `type` or `registrationYear` in the
    // body is ignored rather than rejected or used.
    [Fact]
    [Trait("Scenario", "REG-PUT-07")]
    public async Task PutRegistration_WhenBodyHasTypeAndYear_IgnoresThemAndUsesTheRoute()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        const string payload = """
            { "status": "REGISTERED", "type": "LARGE_PRODUCER", "registrationYear": 2040 }
            """;

        var (response, body) = await PutRegistration(id, "EXPORTER-2030", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().Be($"/organisations/{id}/registrations/EXPORTER-2030");
        using var registration = JsonDocument.Parse(body);
        registration.RootElement.GetProperty("type").GetString().Should().Be("EXPORTER");
        registration.RootElement.GetProperty("registrationYear").GetInt32().Should().Be(2030);
        (await RegistrationKeys(id)).Should().BeEquivalentTo(OriginalKey, "EXPORTER-2030");
    }

    // The route only matches a GUID, so a non-GUID id finds no endpoint: 404 with an empty body,
    // before authentication or body validation run.
    [Theory]
    [Trait("Scenario", "REG-PUT-09")]
    [InlineData(null)]
    [InlineData(nameof(TestClient.Developer))]
    public async Task PutRegistration_WhenIdIsNotGuid_ReturnsNotFoundWithEmptyBody(string? clientName)
    {
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        using var response = await client.PutAsync(
            "/organisations/not-a-guid/registrations/EXPORTER-2030",
            Json(Registered),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    // Characterisation: documents current behaviour, not a requirement
    // Likely bug: enums are bound from their numeric value too, so `1` in the route is read as
    // the second type and stored under its name.
    [Fact]
    [Trait("Scenario", "REG-PUT-13")]
    public async Task PutRegistration_WhenTypeIsANumber_CreatesTheTypeWithThatValue()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await PutRegistration(id, "1-2025", Registered);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().Be($"/organisations/{id}/registrations/LARGE_PRODUCER-2025");
        using var registration = JsonDocument.Parse(body);
        registration.RootElement.GetProperty("type").GetString().Should().Be("LARGE_PRODUCER");
        (await RegistrationKeys(id)).Should().BeEquivalentTo(OriginalKey, "LARGE_PRODUCER-2025");
    }

    // Characterisation: documents current behaviour, not a requirement
    // Likely bug: a numeric status is accepted, and `1` is the second status, CANCELLED.
    [Fact]
    [Trait("Scenario", "REG-PUT-13")]
    public async Task PutRegistration_WhenStatusIsANumber_StoresTheStatusWithThatValue()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await PutRegistration(id, "EXPORTER-2030", """{ "status": 1 }""");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var registration = JsonDocument.Parse(body);
        registration.RootElement.GetProperty("status").GetString().Should().Be("CANCELLED");
        (await StoredStatus(id, "EXPORTER-2030")).Should().Be("CANCELLED");
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

    private static async Task<JsonElement> GetRegistrations(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        return document.RootElement.GetProperty("registrations").Clone();
    }

    /// <summary>The stored registrations as <c>{type}-{registrationYear}</c> keys.</summary>
    private static async Task<List<string>> RegistrationKeys(Guid id) =>
        (await GetRegistrations(id))
            .EnumerateArray()
            .Select(r => $"{r.GetProperty("type").GetString()}-{r.GetProperty("registrationYear").GetInt32()}")
            .ToList();

    private static async Task<string?> StoredStatus(Guid id, string key) =>
        (await GetRegistrations(id))
            .EnumerateArray()
            .Single(r => $"{r.GetProperty("type").GetString()}-{r.GetProperty("registrationYear").GetInt32()}" == key)
            .GetProperty("status")
            .GetString();
}
