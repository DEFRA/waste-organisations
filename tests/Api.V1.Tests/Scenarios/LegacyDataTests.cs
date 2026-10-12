using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using MongoDB.Bson;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// Production holds documents the API can no longer create. Each test inserts its own document
/// through <see cref="LegacyData"/> and then checks the behaviour through the API.
/// </summary>
public class LegacyDataTests : V1TestBase
{
    // Fixed, in the past and with milliseconds, so a fallback value cannot be mistaken for the
    // time of the request, and the millisecond precision BSON keeps is visible in the response.
    private static readonly DateTime OrganisationCreated = new(2024, 3, 1, 9, 30, 0, 123, DateTimeKind.Utc);

    // Registrations stored before the API recorded registration timestamps must still be
    // served, with both timestamps taken from the organisation's own `created`.
    [Fact]
    [Trait("Scenario", "LEG-01")]
    public async Task GetOrganisation_WhenRegistrationsHaveNoTimestamps_ReturnsOrganisationCreatedForBoth()
    {
        var id = Guid.NewGuid();
        await using var legacyData = new LegacyData();
        await legacyData.Insert(
            LegacyData.Organisation(
                id,
                OrganisationCreated,
                LegacyData.RegistrationWithoutTimestamps("SMALL_PRODUCER", 2024, "REGISTERED"),
                LegacyData.RegistrationWithoutTimestamps("EXPORTER", 2025, "CANCELLED")
            )
        );
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        var organisation = document.RootElement;
        organisation.GetProperty("id").GetGuid().Should().Be(id);

        var registrations = organisation.GetProperty("registrations").EnumerateArray().ToList();
        registrations
            .Select(r => (r.GetProperty("type").GetString(), r.GetProperty("registrationYear").GetInt32()))
            .Should()
            .BeEquivalentTo([("SMALL_PRODUCER", 2024), ("EXPORTER", 2025)]);

        foreach (var registration in registrations)
        {
            registration.GetProperty("created").GetDateTimeOffset().Should().Be(OrganisationCreated);
            registration.GetProperty("updated").GetDateTimeOffset().Should().Be(OrganisationCreated);
        }
    }

    [Fact]
    [Trait("Scenario", "LEG-02")]
    public async Task Search_WhenRegistrationsHaveNoTimestamps_ReturnsOrganisationCreatedForBoth()
    {
        var id = Guid.NewGuid();
        await using var legacyData = new LegacyData();
        await legacyData.Insert(LegacyOrganisation(id));
        using var client = CreateClient(TestClient.Developer);

        // Filtered to the legacy registration's type and year so the shared stack's other
        // organisations do not all come back; the whole organisation is still returned.
        using var response = await client.GetAsync(
            "/organisations?registrations=SMALL_PRODUCER&registrationYears=2024",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var organisation = body["organisations"]!
            .AsArray()
            .Should()
            .ContainSingle(o => o!["id"]!.GetValue<Guid>() == id)
            .Subject!;
        ShouldAllFallBackToOrganisationCreated(organisation, "SMALL_PRODUCER-2024", "EXPORTER-2025");
    }

    // The registration in the request equals the stored one, so its missing timestamps are kept
    // missing and still served as the fallback; only the organisation's own `updated` moves.
    [Fact]
    [Trait("Scenario", "LEG-03")]
    public async Task PutOrganisation_WhenSameRegistrationAndStatus_KeepsFallbackAndAdvancesOrganisationUpdated()
    {
        var id = Guid.NewGuid();
        await using var legacyData = new LegacyData();
        await legacyData.Insert(LegacyOrganisation(id));
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.PutAsync(
            $"/organisations/{id}",
            Json(OrganisationPayloads.Full(type: "SMALL_PRODUCER", registrationYear: 2024, status: "REGISTERED")),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        ShouldAllFallBackToOrganisationCreated(body, "SMALL_PRODUCER-2024", "EXPORTER-2025");
        ShouldAllFallBackToOrganisationCreated(await GetOrganisation(id), "SMALL_PRODUCER-2024", "EXPORTER-2025");

        // The API does not return the organisation's own timestamps, so they are read from MongoDB.
        var stored = (await LegacyData.Find(id))!;
        stored["created"].ToUniversalTime().Should().Be(OrganisationCreated);
        stored["updated"].ToUniversalTime().Should().BeAfter(OrganisationCreated);
    }

    [Fact]
    [Trait("Scenario", "LEG-04")]
    public async Task PutRegistration_WhenSameStatus_ReturnsOkAndKeepsFallback()
    {
        var id = Guid.NewGuid();
        await using var legacyData = new LegacyData();
        await legacyData.Insert(LegacyOrganisation(id));

        var (response, registration) = await PutRegistration(id, "SMALL_PRODUCER-2024", "REGISTERED");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Timestamps(registration).Should().Be((OrganisationCreated, OrganisationCreated));
        ShouldAllFallBackToOrganisationCreated(await GetOrganisation(id), "SMALL_PRODUCER-2024", "EXPORTER-2025");
    }

    // A status change gives the registration its first real `updated`, while `created` is
    // filled in from the organisation's `created` rather than from the time of the request.
    [Fact]
    [Trait("Scenario", "LEG-05")]
    public async Task PutRegistration_WhenStatusChanged_KeepsFallbackCreatedAndSetsUpdatedToNow()
    {
        var id = Guid.NewGuid();
        await using var legacyData = new LegacyData();
        await legacyData.Insert(LegacyOrganisation(id));

        var (response, registration) = await PutRegistration(id, "SMALL_PRODUCER-2024", "CANCELLED");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        registration["status"]!.GetValue<string>().Should().Be("CANCELLED");
        var (created, updated) = Timestamps(registration);
        created.Should().Be(OrganisationCreated);
        updated.Should().BeAfter(OrganisationCreated);
        // The API's clock is the Docker host's, so only a coarse bound is safe here.
        updated.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var stored = await GetOrganisation(id);
        Timestamps(Registration(stored, "SMALL_PRODUCER-2024")).Should().Be((created, updated));
        ShouldAllFallBackToOrganisationCreated(stored, "EXPORTER-2025");
    }

    // Older documents were written without the optional fields at all, rather than with them set
    // to null. The API must serve such a document the same way as one with the fields set to null.
    [Fact]
    [Trait("Scenario", "LEG-06")]
    public async Task GetOrganisation_WhenOptionalFieldsMissingFromDocument_ReturnsThemAsExplicitNull()
    {
        var id = Guid.NewGuid();
        var document = LegacyOrganisation(id);
        document.Remove("tradingName");
        document.Remove("businessCountry");
        document.Remove("companiesHouseNumber");
        document["address"] = new BsonDocument();
        await using var legacyData = new LegacyData();
        await legacyData.Insert(document);

        var organisation = (await GetOrganisation(id)).AsObject();

        organisation["id"]!.GetValue<Guid>().Should().Be(id);
        organisation["name"]!.GetValue<string>().Should().Be("V1 Legacy Organisation Ltd");
        foreach (var field in new[] { "tradingName", "businessCountry", "companiesHouseNumber" })
            ShouldBeExplicitNull(organisation, field);
        var address = organisation["address"]!.AsObject();
        foreach (var field in new[] { "addressLine1", "addressLine2", "town", "county", "postcode", "country" })
            ShouldBeExplicitNull(address, field);
        ShouldAllFallBackToOrganisationCreated(organisation, "SMALL_PRODUCER-2024", "EXPORTER-2025");
    }

    // An organisation without registrations is still served by get, as an empty list, but search
    // only returns organisations with at least one registration matching the filters.
    [Theory]
    [Trait("Scenario", "LEG-07")]
    [InlineData("missing")]
    [InlineData("empty")]
    public async Task Organisation_WhenRegistrationsMissingOrEmpty_GetReturnsEmptyListAndSearchNeverReturnsIt(
        string registrationsCase
    )
    {
        var id = Guid.NewGuid();
        var document = LegacyOrganisation(id);
        if (registrationsCase == "missing")
            document.Remove("registrations");
        else
            document["registrations"] = new BsonArray();
        await using var legacyData = new LegacyData();
        await legacyData.Insert(document);

        var organisation = await GetOrganisation(id);

        organisation["id"]!.GetValue<Guid>().Should().Be(id);
        organisation["registrations"]!.AsArray().Should().BeEmpty();
        using var client = CreateClient(TestClient.Developer);
        foreach (
            var query in new[]
            {
                "/organisations",
                "/organisations?registrations=SMALL_PRODUCER,EXPORTER",
                "/organisations?registrationYears=2024,2025",
                "/organisations?statuses=REGISTERED,CANCELLED",
            }
        )
        {
            using var response = await client.GetAsync(query, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
            body["organisations"]!
                .AsArray()
                .Select(o => o!["id"]!.GetValue<Guid>())
                .Should()
                .NotContain(id, $"\"{query}\" must not return it");
        }
    }

    private static void ShouldBeExplicitNull(JsonObject parent, string field)
    {
        parent.ContainsKey(field).Should().BeTrue($"{field} must be present");
        parent[field].Should().BeNull(field);
    }

    /// <summary>A legacy organisation with two registrations, neither with timestamps.</summary>
    private static BsonDocument LegacyOrganisation(Guid id) =>
        LegacyData.Organisation(
            id,
            OrganisationCreated,
            LegacyData.RegistrationWithoutTimestamps("SMALL_PRODUCER", 2024, "REGISTERED"),
            LegacyData.RegistrationWithoutTimestamps("EXPORTER", 2025, "CANCELLED")
        );

    private static void ShouldAllFallBackToOrganisationCreated(JsonNode organisation, params string[] keys)
    {
        foreach (var key in keys)
            Timestamps(Registration(organisation, key))
                .Should()
                .Be((OrganisationCreated, OrganisationCreated), $"{key} has no stored timestamps");
    }

    private static JsonNode Registration(JsonNode organisation, string key) =>
        organisation["registrations"]!
            .AsArray()
            .Should()
            .ContainSingle(r => $"{r!["type"]}-{r["registrationYear"]}" == key)
            .Subject!;

    private static (DateTimeOffset Created, DateTimeOffset Updated) Timestamps(JsonNode registration) =>
        (registration["created"]!.GetValue<DateTimeOffset>(), registration["updated"]!.GetValue<DateTimeOffset>());

    private static async Task<JsonNode> GetOrganisation(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<(HttpResponseMessage Response, JsonNode Registration)> PutRegistration(
        Guid id,
        string key,
        string status
    )
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.PutAsync(
            $"/organisations/{id}/registrations/{key}",
            Json($$"""{ "status": "{{status}}" }"""),
            TestContext.Current.CancellationToken
        );

        return (
            response,
            JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!
        );
    }
}
