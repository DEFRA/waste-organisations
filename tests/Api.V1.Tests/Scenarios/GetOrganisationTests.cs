using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class GetOrganisationTests : V1TestBase
{
    [Fact]
    [Trait("Scenario", "ORG-GET-01")]
    public async Task GetOrganisation_WhenExists_ReturnsFullPayload()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        using var client = CreateClient(TestClient.Developer);

        var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Scenario", "ORG-GET-02")]
    public async Task GetOrganisation_WhenUnknownId_ReturnsNotFoundWithEmptyBody()
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.GetAsync($"/organisations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "ORG-GET-02")]
    public async Task GetOrganisation_WhenEmptyGuid_ReturnsNotFoundWithEmptyBody()
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.GetAsync($"/organisations/{Guid.Empty}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    // The route only matches a GUID, so a non-GUID id finds no endpoint and authentication never
    // runs: the caller gets 404 rather than 401 even without credentials.
    [Theory]
    [Trait("Scenario", "ORG-GET-03")]
    [InlineData(null)]
    [InlineData(nameof(TestClient.Developer))]
    public async Task GetOrganisation_WhenIdIsNotGuid_ReturnsNotFoundWithEmptyBody(string? clientName)
    {
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        var response = await client.GetAsync("/organisations/not-a-guid", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    // Clients may rely on every property being present, so unset optional fields must come back
    // as explicit nulls rather than being left out of the body.
    [Fact]
    [Trait("Scenario", "ORG-GET-05")]
    public async Task GetOrganisation_WhenOptionalFieldsNeverSet_ReturnsThemAsExplicitNull()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Minimal());
        using var client = CreateClient(TestClient.Developer);

        var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        var organisation = document.RootElement;
        var address = organisation.GetProperty("address");
        string[] organisationFields = ["tradingName", "businessCountry", "companiesHouseNumber"];
        string[] addressFields = ["addressLine1", "addressLine2", "town", "county", "postcode", "country"];
        foreach (var field in organisationFields)
        {
            organisation.GetProperty(field).ValueKind.Should().Be(JsonValueKind.Null, field);
        }
        foreach (var field in addressFields)
        {
            address.GetProperty(field).ValueKind.Should().Be(JsonValueKind.Null, $"address.{field}");
        }

        await VerifyJson(body);
    }

    [Theory]
    [Trait("Scenario", "ORG-GET-08")]
    [InlineData("upper-case")]
    [InlineData("no-hyphens")]
    public async Task GetOrganisation_WhenIdInOtherGuidFormat_ReturnsSameOrganisation(string format)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        var routeId = format switch
        {
            "upper-case" => id.ToString("D").ToUpperInvariant(),
            "no-hyphens" => id.ToString("N"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "No such GUID format"),
        };
        using var client = CreateClient(TestClient.Developer);

        var response = await client.GetAsync($"/organisations/{routeId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        document.RootElement.GetProperty("id").GetString().Should().Be(id.ToString());
    }
}
