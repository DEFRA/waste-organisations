using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// Which callers the registration put lets through: it needs the write scope, and a client with
/// only the read scope is refused rather than unauthenticated.
/// </summary>
public class PutRegistrationAuthTests : V1TestBase
{
    // The organisation exists in every case, so a refusal cannot be a 404 in disguise. A refused
    // put must also leave nothing behind, so the stored registrations are checked with a
    // full-scope client afterwards.
    [Theory]
    [Trait("Scenario", "REG-PUT-10")]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(nameof(TestClient.ReadOnlyApiKey), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.ReadOnlyOAuth), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.WriteOnlyApiKey), HttpStatusCode.Created)]
    [InlineData(nameof(TestClient.WriteOnlyOAuth), HttpStatusCode.Created)]
    public async Task PutRegistration_ByClientScope_ReturnsExpectedStatusAndStoresOnlyOnSuccess(
        string? clientName,
        HttpStatusCode expected
    )
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        using var response = await client.PutAsync(
            $"/organisations/{id}/registrations/EXPORTER-2030",
            Json("""{ "status": "REGISTERED" }"""),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(expected);
        var stored = await StoredRegistrationTypes(id);
        if (expected == HttpStatusCode.Created)
            stored.Should().BeEquivalentTo("SMALL_PRODUCER", "EXPORTER");
        else
            stored.Should().BeEquivalentTo("SMALL_PRODUCER");
    }

    private static async Task<List<string>> StoredRegistrationTypes(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);
        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );

        return document
            .RootElement.GetProperty("registrations")
            .EnumerateArray()
            .Select(r => r.GetProperty("type").GetString()!)
            .ToList();
    }
}
