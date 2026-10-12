using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class PutOrganisationBodyEdgeCaseTests : V1TestBase
{
    private const string MalformedBody = """{"name": "V1 Malformed Ltd",""";

    // Characterisation: documents current behaviour, not a requirement
    // The body fails to parse before the handler runs, so the 400 `detail` comes from the JSON
    // reader and includes the position where the input ended.
    [Fact]
    [Trait("Scenario", "ORG-PUT-11")]
    public async Task PutOrganisation_WhenBodyIsMalformedJson_ReturnsBadRequestAndStoresNothing()
    {
        var id = Guid.NewGuid();

        var (response, body) = await PutOrganisation(id, Json(MalformedBody));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNotStored(id);
        await VerifyJson(body);
    }

    // Characterisation: documents current behaviour, not a requirement
    [Fact]
    [Trait("Scenario", "ORG-PUT-11")]
    public async Task PutOrganisation_WhenBodyIsEmpty_ReturnsBadRequestAndStoresNothing()
    {
        var id = Guid.NewGuid();

        var (response, body) = await PutOrganisation(id, Json(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNotStored(id);
        await VerifyJson(body);
    }

    // Characterisation: documents current behaviour, not a requirement
    // A valid organisation sent as text/plain is refused on its content type alone, with no
    // problem details body.
    [Fact]
    [Trait("Scenario", "ORG-PUT-11")]
    public async Task PutOrganisation_WhenContentTypeIsNotJson_ReturnsUnsupportedMediaTypeWithEmptyBody()
    {
        var id = Guid.NewGuid();
        var content = new StringContent(OrganisationPayloads.Full(), Encoding.UTF8, "text/plain");

        var (response, body) = await PutOrganisation(id, content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        body.Should().BeEmpty();
        await AssertNotStored(id);
    }

    // Characterisation: documents current behaviour, not a requirement
    // Unknown properties at every level are ignored rather than rejected, and are not returned.
    [Fact]
    [Trait("Scenario", "ORG-PUT-11")]
    public async Task PutOrganisation_WhenBodyHasUnknownProperties_IgnoresThemAndCreates()
    {
        var id = Guid.NewGuid();
        const string payload = """
            {
              "name": "V1 Unknown Properties Ltd",
              "unknownTopLevel": "ignored",
              "address": { "postcode": "TE1 1ST", "unknownInAddress": 1 },
              "registration": {
                "status": "REGISTERED",
                "type": "SMALL_PRODUCER",
                "registrationYear": 2025,
                "unknownInRegistration": true
              }
            }
            """;

        var (response, body) = await PutOrganisation(id, Json(payload));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var stored = await GetOrganisation(id);
        Property(stored, "name").GetString().Should().Be("V1 Unknown Properties Ltd");
        Property(stored, "address", "postcode").GetString().Should().Be("TE1 1ST");
        AssertSingleRegistration(stored);
        stored.Should().NotContain("unknown");
        body.Should().NotContain("unknown");
    }

    // Characterisation: documents current behaviour, not a requirement
    // Property names are matched case-insensitively, so differently cased names are read as the
    // documented ones. Enum values are not covered here: they stay in their documented case.
    [Fact]
    [Trait("Scenario", "ORG-PUT-11")]
    public async Task PutOrganisation_WhenPropertyNamesInAnotherCase_ReadsThemAsTheDocumentedOnes()
    {
        var id = Guid.NewGuid();
        const string payload = """
            {
              "NAME": "V1 Cased Properties Ltd",
              "Address": { "POSTCODE": "TE1 1ST" },
              "Registration": {
                "STATUS": "REGISTERED",
                "Type": "SMALL_PRODUCER",
                "REGISTRATIONYEAR": 2025
              }
            }
            """;

        var (response, _) = await PutOrganisation(id, Json(payload));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var stored = await GetOrganisation(id);
        Property(stored, "name").GetString().Should().Be("V1 Cased Properties Ltd");
        Property(stored, "address", "postcode").GetString().Should().Be("TE1 1ST");
        AssertSingleRegistration(stored);
    }

    // Characterisation: documents current behaviour, not a requirement
    // The GET response shape (`registrations` array) is not accepted on PUT: the array is ignored
    // as an unknown property, and the 400 reports the missing `registration`.
    [Fact]
    [Trait("Scenario", "ORG-PUT-11")]
    public async Task PutOrganisation_WhenRegistrationsArrayInsteadOfRegistration_ReturnsBadRequestAndStoresNothing()
    {
        var id = Guid.NewGuid();
        const string payload = """
            {
              "name": "V1 Registrations Array Ltd",
              "address": {},
              "registrations": [
                { "status": "REGISTERED", "type": "SMALL_PRODUCER", "registrationYear": 2025 }
              ]
            }
            """;

        var (response, body) = await PutOrganisation(id, Json(payload));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNotStored(id);
        await VerifyJson(body);
    }

    private static async Task<(HttpResponseMessage Response, string Body)> PutOrganisation(Guid id, HttpContent content)
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.PutAsync($"/organisations/{id}", content, TestContext.Current.CancellationToken);

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

    private static void AssertSingleRegistration(string organisationBody)
    {
        var registrations = Property(organisationBody, "registrations");
        registrations.GetArrayLength().Should().Be(1);

        var registration = registrations[0];
        registration.GetProperty("status").GetString().Should().Be("REGISTERED");
        registration.GetProperty("type").GetString().Should().Be("SMALL_PRODUCER");
        registration.GetProperty("registrationYear").GetInt32().Should().Be(2025);
    }

    private static JsonElement Property(string body, params string[] path)
    {
        using var document = JsonDocument.Parse(body);

        return path.Aggregate(document.RootElement, (element, name) => element.GetProperty(name)).Clone();
    }
}
