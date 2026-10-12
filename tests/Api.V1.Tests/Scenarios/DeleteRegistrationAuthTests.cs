using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// Which callers the registration delete lets through: it needs the write scope, and a client with
/// only the read scope is refused rather than unauthenticated.
/// </summary>
public class DeleteRegistrationAuthTests : V1TestBase
{
    // The organisation and registration exist in every case, so a refusal cannot be a 404 in
    // disguise. A refused delete must also remove nothing, so the stored registrations are checked
    // with a full-scope client afterwards.
    [Theory]
    [Trait("Scenario", "REG-DEL-10")]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(nameof(TestClient.ReadOnlyApiKey), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.ReadOnlyOAuth), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.WriteOnlyApiKey), HttpStatusCode.NoContent)]
    [InlineData(nameof(TestClient.WriteOnlyOAuth), HttpStatusCode.NoContent)]
    public async Task DeleteRegistration_ByClientScope_ReturnsExpectedStatusAndRemovesOnlyOnSuccess(
        string? clientName,
        HttpStatusCode expected
    )
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        using var response = await client.DeleteAsync(
            $"/organisations/{id}/registrations/SMALL_PRODUCER-2025",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(expected);
        var stored = await StoredRegistrationTypes(id);
        if (expected == HttpStatusCode.NoContent)
            stored.Should().BeEmpty();
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
