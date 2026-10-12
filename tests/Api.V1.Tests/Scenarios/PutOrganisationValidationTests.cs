using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class PutOrganisationValidationTests : V1TestBase
{
    // 2022 and 2051 are just outside the inclusive 2023 to 2050 range. The year is checked by
    // validation after the body is read, so the error comes back in the `errors` dictionary.
    [Theory]
    [Trait("Scenario", "ORG-PUT-07")]
    [InlineData(2022)]
    [InlineData(2051)]
    public async Task PutOrganisation_WhenYearOutOfRange_ReturnsBadRequestAndStoresNothing(int year)
    {
        var id = Guid.NewGuid();

        var (response, body) = await PutOrganisation(id, OrganisationPayloads.Full(registrationYear: year));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNotStored(id);
        await VerifyJson(body).UseParameters(year);
    }

    // An unknown enum value fails while the body is read, so the error is a `detail` naming the
    // JSON path rather than an `errors` dictionary.
    [Theory]
    [Trait("Scenario", "ORG-PUT-08")]
    [InlineData("businessCountry")]
    [InlineData("type")]
    [InlineData("status")]
    public async Task PutOrganisation_WhenEnumValueInvalid_ReturnsBadRequestAndStoresNothing(string field)
    {
        var id = Guid.NewGuid();
        var payload = field switch
        {
            "businessCountry" => OrganisationPayloads.Full(businessCountry: "GB-XXX"),
            "type" => OrganisationPayloads.Full(type: "NOT_A_TYPE"),
            "status" => OrganisationPayloads.Full(status: "NOT_A_STATUS"),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "No such field"),
        };

        var (response, body) = await PutOrganisation(id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNotStored(id);
        await VerifyJson(body).UseParameters(field);
    }

    [Theory]
    [Trait("Scenario", "ORG-PUT-09")]
    [InlineData("GB-ENG")]
    [InlineData("GB-NIR")]
    [InlineData("GB-SCT")]
    [InlineData("GB-WLS")]
    [InlineData(null)]
    public async Task PutOrganisation_WhenBusinessCountryValidOrNull_StoresAndReturnsItUnchanged(
        string? businessCountry
    )
    {
        var id = Guid.NewGuid();

        var (response, body) = await PutOrganisation(id, OrganisationPayloads.Full(businessCountry: businessCountry));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        BusinessCountry(body).Should().Be(businessCountry);
        BusinessCountry(await GetOrganisation(id)).Should().Be(businessCountry);
    }

    // Each required property is removed in turn from an otherwise valid body; the 400 names the
    // missing property and, for the registration's own fields, the path to it.
    [Theory]
    [Trait("Scenario", "ORG-PUT-10")]
    [InlineData("name")]
    [InlineData("address")]
    [InlineData("registration")]
    [InlineData("registration.status")]
    [InlineData("registration.type")]
    [InlineData("registration.registrationYear")]
    public async Task PutOrganisation_WhenRequiredFieldMissing_ReturnsBadRequestNamingItAndStoresNothing(string field)
    {
        var id = Guid.NewGuid();

        var (response, body) = await PutOrganisation(id, Without(OrganisationPayloads.Full(), field));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNotStored(id);
        await VerifyJson(body).UseParameters(field);
    }

    // The route only matches a GUID, so a non-GUID id finds no endpoint: 404 with an empty body,
    // before authentication or body validation run.
    [Theory]
    [Trait("Scenario", "ORG-PUT-12")]
    [InlineData(null)]
    [InlineData(nameof(TestClient.Developer))]
    public async Task PutOrganisation_WhenIdIsNotGuid_ReturnsNotFoundWithEmptyBody(string? clientName)
    {
        using var client = clientName is null ? CreateClient() : CreateClient(TestClient.Named(clientName));

        using var response = await client.PutAsync(
            "/organisations/not-a-guid",
            Json(OrganisationPayloads.Full()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    /// <summary>Removes a property, given as a dotted path, from a raw JSON body.</summary>
    private static string Without(string body, string path)
    {
        var root = JsonNode.Parse(body)!.AsObject();
        var segments = path.Split('.');
        var parent = segments[..^1].Aggregate(root, (node, segment) => node[segment]!.AsObject());

        parent.Remove(segments[^1]).Should().BeTrue($"the payload should contain {path}");

        return root.ToJsonString();
    }

    private static async Task<(HttpResponseMessage Response, string Body)> PutOrganisation(Guid id, string body)
    {
        using var client = CreateClient(TestClient.Developer);

        var response = await client.PutAsync($"/organisations/{id}", Json(body), TestContext.Current.CancellationToken);

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

    private static string? BusinessCountry(string organisationBody)
    {
        using var document = JsonDocument.Parse(organisationBody);

        return document.RootElement.GetProperty("businessCountry").GetString();
    }
}
