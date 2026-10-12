using System.Net;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// Which callers each organisation endpoint lets through: get needs the read scope, put needs
/// the write scope, and a client with only the other scope is refused rather than unauthenticated.
/// </summary>
public class OrganisationAuthTests : V1TestBase
{
    // The organisation exists in every case, so a refusal cannot be a 404 in disguise.
    [Theory]
    [Trait("Scenario", "ORG-GET-06")]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(nameof(TestClient.WriteOnlyApiKey), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.WriteOnlyOAuth), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.ReadOnlyApiKey), HttpStatusCode.OK)]
    [InlineData(nameof(TestClient.ReadOnlyOAuth), HttpStatusCode.OK)]
    public async Task GetOrganisation_ByClientScope_ReturnsExpectedStatus(string? clientName, HttpStatusCode expected)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(expected);
    }

    // A refused put must also leave nothing behind, so the stored state is checked with a
    // full-scope client afterwards.
    [Theory]
    [Trait("Scenario", "ORG-PUT-15")]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(nameof(TestClient.ReadOnlyApiKey), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.ReadOnlyOAuth), HttpStatusCode.Forbidden)]
    [InlineData(nameof(TestClient.WriteOnlyApiKey), HttpStatusCode.Created)]
    [InlineData(nameof(TestClient.WriteOnlyOAuth), HttpStatusCode.Created)]
    public async Task PutOrganisation_ByClientScope_ReturnsExpectedStatusAndStoresOnlyOnSuccess(
        string? clientName,
        HttpStatusCode expected
    )
    {
        var id = Guid.NewGuid();
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        using var response = await client.PutAsync(
            $"/organisations/{id}",
            Json(OrganisationPayloads.Full()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(expected);
        (await GetStatus(id))
            .Should()
            .Be(expected == HttpStatusCode.Created ? HttpStatusCode.OK : HttpStatusCode.NotFound);
    }

    private static async Task<HttpStatusCode> GetStatus(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        return response.StatusCode;
    }
}
