using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class PutOrganisationTextAndTypeTests : V1TestBase
{
    [Fact]
    [Trait("Scenario", "ORG-PUT-17")]
    public async Task PutOrganisation_WhenTextHasUnicodeAndSpecialCharacters_StoresAndReturnsThemUnchanged()
    {
        var id = Guid.NewGuid();
        // JSON escapes cover characters a client must escape (quote, backslash, control
        // characters); the rest are sent as literal UTF-8, including one outside the BMP.
        const string payload = """
            {
              "name": "Zoë & Søn <Ltd> \"Quoted\" \\ 日本 🚀",
              "tradingName": "Tab\there\nnew line",
              "address": {
                "addressLine1": "Rue de l’Église",
                "town": "Ystradgynlais – Cwm",
                "postcode": "TE1 1ST"
              },
              "registration": { "status": "REGISTERED", "type": "SMALL_PRODUCER", "registrationYear": 2025 }
            }
            """;

        var (response, body) = await PutOrganisation(id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var stored = await GetOrganisation(id);
        foreach (var returned in new[] { body, stored })
        {
            Property(returned, "name").GetString().Should().Be("Zoë & Søn <Ltd> \"Quoted\" \\ 日本 🚀");
            Property(returned, "tradingName").GetString().Should().Be("Tab\there\nnew line");
            Property(returned, "address", "addressLine1").GetString().Should().Be("Rue de l’Église");
            Property(returned, "address", "town").GetString().Should().Be("Ystradgynlais – Cwm");
        }
    }

    // Characterisation: documents current behaviour, not a requirement
    // `address` is required but only its presence is checked, so an explicit null gets past
    // deserialisation and fails later as an unhandled 500. Likely bug: a 400 would be expected.
    [Fact]
    [Trait("Scenario", "ORG-PUT-18")]
    public async Task PutOrganisation_WhenAddressIsNull_ReturnsInternalServerErrorAndStoresNothing()
    {
        var id = Guid.NewGuid();
        const string payload = """
            {
              "name": "V1 Null Address Ltd",
              "address": null,
              "registration": { "status": "REGISTERED", "type": "SMALL_PRODUCER", "registrationYear": 2025 }
            }
            """;

        var (response, _) = await PutOrganisation(id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await AssertNotStored(id);
    }

    // Characterisation: documents current behaviour, not a requirement
    // `name` is required but only its presence is checked, so a null or empty name is stored as
    // sent. Likely bug: an organisation without a name would be expected to be rejected.
    [Theory]
    [Trait("Scenario", "ORG-PUT-19")]
    [InlineData(null)]
    [InlineData("")]
    public async Task PutOrganisation_WhenNameIsNullOrEmpty_CreatesAndStoresItAsSent(string? name)
    {
        var id = Guid.NewGuid();
        var nameJson = name is null ? "null" : $"\"{name}\"";
        var payload = $$"""
            {
              "name": {{nameJson}},
              "address": {},
              "registration": { "status": "REGISTERED", "type": "SMALL_PRODUCER", "registrationYear": 2025 }
            }
            """;

        var (response, _) = await PutOrganisation(id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var storedName = Property(await GetOrganisation(id), "name");
        if (name is null)
        {
            storedName.ValueKind.Should().Be(JsonValueKind.Null);
        }
        else
        {
            storedName.GetString().Should().Be(name);
        }
    }

    // Characterisation: documents current behaviour, not a requirement
    // Enums are read from their underlying number as well as their name, so `1` is taken as the
    // second registration type. Likely bug: the documented contract is the string name only.
    [Fact]
    [Trait("Scenario", "ORG-PUT-20")]
    public async Task PutOrganisation_WhenTypeIsANumber_StoresTheMatchingTypeName()
    {
        var id = Guid.NewGuid();
        const string payload = """
            {
              "name": "V1 Numeric Type Ltd",
              "address": {},
              "registration": { "status": "REGISTERED", "type": 1, "registrationYear": 2025 }
            }
            """;

        var (response, _) = await PutOrganisation(id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var registration = Property(await GetOrganisation(id), "registrations")[0];
        registration.GetProperty("type").GetString().Should().Be("LARGE_PRODUCER");
    }

    // Characterisation: documents current behaviour, not a requirement
    // Numbers are also read from strings, so a quoted year is accepted. Likely bug: the
    // documented contract is a JSON number.
    [Fact]
    [Trait("Scenario", "ORG-PUT-20")]
    public async Task PutOrganisation_WhenYearIsAString_AcceptsItAsTheNumber()
    {
        var id = Guid.NewGuid();
        const string payload = """
            {
              "name": "V1 String Year Ltd",
              "address": {},
              "registration": { "status": "REGISTERED", "type": "SMALL_PRODUCER", "registrationYear": "2025" }
            }
            """;

        var (response, _) = await PutOrganisation(id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var registration = Property(await GetOrganisation(id), "registrations")[0];
        registration.GetProperty("registrationYear").GetInt32().Should().Be(2025);
    }

    private static async Task<(HttpResponseMessage Response, string Body)> PutOrganisation(Guid id, string payload)
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.PutAsync(
            $"/organisations/{id}",
            Json(payload),
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

    /// <summary>A rejected PUT to a new id must not create the organisation.</summary>
    private static async Task AssertNotStored(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static JsonElement Property(string body, params string[] path)
    {
        using var document = JsonDocument.Parse(body);

        return path.Aggregate(document.RootElement, (element, name) => element.GetProperty(name)).Clone();
    }
}
