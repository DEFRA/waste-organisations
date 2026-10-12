using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

// Every organisation here is created with SMALL_PRODUCER-2025 unless the test says otherwise.
public class DeleteRegistrationTests : V1TestBase
{
    private const string Registered = """{ "status": "REGISTERED" }""";

    // The organisation's own fields and every other registration must come back exactly as they
    // were before the delete.
    [Fact]
    [Trait("Scenario", "REG-DEL-01")]
    public async Task DeleteRegistration_WhenOrganisationAndRegistrationExist_ReturnsNoContentAndRemovesOnlyThatRegistration()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        await PutRegistration(id, "EXPORTER-2030", HttpStatusCode.Created);
        await PutRegistration(id, "COMPLIANCE_SCHEME-2027", HttpStatusCode.Created);
        var before = JsonNode.Parse(await GetOrganisation(id))!.AsObject();

        var (response, body) = await DeleteRegistration(id, "EXPORTER-2030");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        body.Should().BeEmpty();
        var after = JsonNode.Parse(await GetOrganisation(id))!.AsObject();
        foreach (var (name, value) in before.Where(p => p.Key != "registrations"))
            JsonNode.DeepEquals(after[name], value).Should().BeTrue($"\"{name}\" must be unchanged");
        Registrations(after)
            .Should()
            .BeEquivalentTo(
                Registrations(before).Where(r => !r.StartsWith("EXPORTER-2030 ", StringComparison.Ordinal))
            );
    }

    [Fact]
    [Trait("Scenario", "REG-DEL-02")]
    public async Task DeleteRegistration_WhenOrganisationUnknown_ReturnsNotFound()
    {
        var (response, body) = await DeleteRegistration(Guid.NewGuid(), "SMALL_PRODUCER-2025");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    // Neither the type nor the year matches: the organisation only has SMALL_PRODUCER-2025.
    [Theory]
    [Trait("Scenario", "REG-DEL-03")]
    [InlineData("EXPORTER-2025")]
    [InlineData("SMALL_PRODUCER-2026")]
    public async Task DeleteRegistration_WhenRegistrationNotFound_ReturnsNotFoundAndKeepsTheOthers(string key)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        var before = await GetOrganisation(id);

        var (response, body) = await DeleteRegistration(id, key);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
        (await GetOrganisation(id)).Should().Be(before);
    }

    [Fact]
    [Trait("Scenario", "REG-DEL-04")]
    public async Task DeleteRegistration_WhenSentTwice_ReturnsNoContentThenNotFound()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        await PutRegistration(id, "EXPORTER-2030", HttpStatusCode.Created);

        var (first, _) = await DeleteRegistration(id, "EXPORTER-2030");
        var (second, body) = await DeleteRegistration(id, "EXPORTER-2030");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    // The organisation itself survives with no registrations, but search only returns
    // organisations through their registrations, so it can no longer be found that way.
    [Fact]
    [Trait("Scenario", "REG-DEL-05")]
    public async Task DeleteRegistration_WhenLastRegistration_LeavesEmptyRegistrationsAndHidesFromSearch()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());

        var (response, _) = await DeleteRegistration(id, "SMALL_PRODUCER-2025");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var organisation = JsonNode.Parse(await GetOrganisation(id))!.AsObject();
        organisation["registrations"]!.AsArray().Should().BeEmpty();
        (await SearchIds("/organisations")).Should().NotContain(id);
        (await SearchIds("/organisations?registrations=SMALL_PRODUCER")).Should().NotContain(id);
        (await SearchIds("/organisations?registrationYears=2025")).Should().NotContain(id);
        (await SearchIds("/organisations?statuses=REGISTERED")).Should().NotContain(id);
    }

    [Fact]
    [Trait("Scenario", "REG-DEL-08")]
    public async Task PutRegistration_WhenRecreatingADeletedRegistration_ReturnsCreatedWithNewCreated()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        var original = await PutRegistration(id, "EXPORTER-2030", HttpStatusCode.Created);
        var (deleted, _) = await DeleteRegistration(id, "EXPORTER-2030");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Wait();

        var recreated = await PutRegistration(id, "EXPORTER-2030", HttpStatusCode.Created);

        recreated.Created.Should().BeAfter(original.Created);
        recreated.Updated.Should().Be(recreated.Created);
    }

    [Theory]
    [Trait("Scenario", "REG-DEL-12")]
    [InlineData("SMALL_PRODUCER")]
    [InlineData("LARGE_PRODUCER")]
    [InlineData("COMPLIANCE_SCHEME")]
    [InlineData("REPROCESSOR")]
    [InlineData("EXPORTER")]
    public async Task DeleteRegistration_WhenEachType_ReturnsNoContent(string type)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full(type: type));

        var (response, _) = await DeleteRegistration(id, $"{type}-2025");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var organisation = JsonNode.Parse(await GetOrganisation(id))!.AsObject();
        organisation["registrations"]!.AsArray().Should().BeEmpty();
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

    private static async Task<Timestamps> PutRegistration(Guid id, string key, HttpStatusCode expected)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.PutAsync(
            $"/organisations/{id}/registrations/{key}",
            Json(Registered),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(expected);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        return new(
            document.RootElement.GetProperty("created").GetDateTimeOffset(),
            document.RootElement.GetProperty("updated").GetDateTimeOffset()
        );
    }

    private static async Task<string> GetOrganisation(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<Guid>> SearchIds(string path)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        return document
            .RootElement.GetProperty("organisations")
            .EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid())
            .ToList();
    }

    // "TYPE-YEAR " followed by the whole registration, so a removed key is easy to filter out.
    private static IEnumerable<string> Registrations(JsonObject organisation) =>
        organisation["registrations"]!
            .AsArray()
            .Select(r => $"{r!["type"]}-{r["registrationYear"]} {r.ToJsonString()}");

    private sealed record Timestamps(DateTimeOffset Created, DateTimeOffset Updated);
}
