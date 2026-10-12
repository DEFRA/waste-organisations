using System.Net;
using System.Text.Json;
using AwesomeAssertions;

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
}
