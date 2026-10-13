using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

// Every organisation here is created with SMALL_PRODUCER-2025 only, so a rejected delete is
// shown to have removed nothing when that is still its one registration.
public class DeleteRegistrationValidationTests : V1TestBase
{
    private const string OriginalKey = "SMALL_PRODUCER-2025";

    // An unknown type fails while the route value is bound, so the 400 is a `detail`; a year
    // outside the inclusive 2023 to 2050 range fails validation, so it is an `errors` dictionary.
    // Both are checked before the organisation is looked up.
    [Theory]
    [Trait("Scenario", "REG-DEL-06")]
    [InlineData("NOT_A_TYPE-2025")]
    [InlineData("SMALL_PRODUCER-2022")]
    [InlineData("SMALL_PRODUCER-2051")]
    public async Task DeleteRegistration_WhenTypeOrYearInvalid_ReturnsBadRequestAndRemovesNothing(string key)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await DeleteRegistration(id, key);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RegistrationKeys(id)).Should().Equal(OriginalKey);
        await VerifyJson(body).UseParameters(key);
    }

    // The route only matches an integer year, so a non-numeric year finds no endpoint at all.
    [Fact]
    [Trait("Scenario", "REG-DEL-07")]
    public async Task DeleteRegistration_WhenYearIsNotANumber_ReturnsNotFoundWithEmptyBody()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, body) = await DeleteRegistration(id, "SMALL_PRODUCER-abc");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
        (await RegistrationKeys(id)).Should().Equal(OriginalKey);
    }

    // The route only matches a GUID, so a non-GUID id finds no endpoint: 404 with an empty body,
    // before authentication runs.
    [Theory]
    [Trait("Scenario", "REG-DEL-07")]
    [InlineData(null)]
    [InlineData(nameof(TestClient.Developer))]
    public async Task DeleteRegistration_WhenIdIsNotGuid_ReturnsNotFoundWithEmptyBody(string? clientName)
    {
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        using var response = await client.DeleteAsync(
            "/organisations/not-a-guid/registrations/SMALL_PRODUCER-2025",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static async Task<(HttpResponseMessage Response, string Body)> DeleteRegistration(Guid id, string key)
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.DeleteAsync(
            $"/organisations/{id}/registrations/{key}",
            TestContext.Current.CancellationToken
        );

        return (response, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>The stored registrations as <c>{type}-{registrationYear}</c> keys.</summary>
    private static async Task<List<string>> RegistrationKeys(Guid id)
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
            .Select(r => $"{r.GetProperty("type").GetString()}-{r.GetProperty("registrationYear").GetInt32()}")
            .ToList();
    }
}
