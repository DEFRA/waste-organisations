using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

// One organisation taken through the client-visible life of its registrations. After every change
// the get must show exactly the registrations the test expects, and every search filter must find
// the organisation exactly when one of those registrations meets it, returning what get returns.
// The stack is shared, so only this test's own id is checked in search results.
public class LifecycleTests : V1TestBase
{
    // Each type gets its own year, so a year filter and a type filter pick out the same registration.
    private static readonly (string Type, int Year)[] AllTypes =
    [
        ("SMALL_PRODUCER", 2025),
        ("LARGE_PRODUCER", 2026),
        ("COMPLIANCE_SCHEME", 2027),
        ("REPROCESSOR", 2028),
        ("EXPORTER", 2029),
    ];

    private static readonly string[] AllStatuses = ["REGISTERED", "CANCELLED"];

    [Fact]
    [Trait("Scenario", "X-DATA-01")]
    public async Task Lifecycle_WhenRegistrationsAddedChangedAndDeleted_GetAndSearchStayConsistent()
    {
        var expected = new Dictionary<string, string> { ["SMALL_PRODUCER-2025"] = "REGISTERED" };
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        await AssertConsistent(id, expected);

        foreach (var (type, year) in AllTypes.Skip(1))
        {
            await PutRegistration(id, $"{type}-{year}", "REGISTERED", HttpStatusCode.Created);
            expected[$"{type}-{year}"] = "REGISTERED";
        }
        await AssertConsistent(id, expected);

        await PutRegistration(id, "EXPORTER-2029", "CANCELLED", HttpStatusCode.OK);
        expected["EXPORTER-2029"] = "CANCELLED";
        await AssertConsistent(id, expected);

        await DeleteRegistration(id, "REPROCESSOR-2028");
        expected.Remove("REPROCESSOR-2028");
        await AssertConsistent(id, expected);
    }

    // `expected` maps "TYPE-YEAR" to status. Single filters cover every type, year and status the
    // test uses; the combined filters must be met by one registration, so they also check that
    // values from different registrations are not mixed.
    private static async Task AssertConsistent(Guid id, Dictionary<string, string> expected)
    {
        var organisation = await GetOrganisation(id);
        var registrations = organisation["registrations"]!
            .AsArray()
            .ToDictionary(r => $"{r!["type"]}-{r["registrationYear"]}", r => r!["status"]!.GetValue<string>());
        registrations.Should().BeEquivalentTo(expected);

        var queries = new List<(string Query, Func<string, int, string, bool> Matches)> { ("", (_, _, _) => true) };
        foreach (var (type, year) in AllTypes)
        {
            queries.Add(($"registrations={type}", (t, _, _) => t == type));
            queries.Add(($"registrationYears={year}", (_, y, _) => y == year));
        }
        foreach (var status in AllStatuses)
        {
            queries.Add(($"statuses={status}", (_, _, s) => s == status));
            queries.Add(($"registrations=EXPORTER&statuses={status}", (t, _, s) => t == "EXPORTER" && s == status));
            queries.Add(
                ($"registrationYears=2025,2029&statuses={status}", (_, y, s) => y is 2025 or 2029 && s == status)
            );
        }

        foreach (var (query, matches) in queries)
        {
            var shouldMatch = expected.Any(r =>
            {
                var separator = r.Key.LastIndexOf('-');
                return matches(r.Key[..separator], int.Parse(r.Key[(separator + 1)..]), r.Value);
            });

            var results = await Search($"/organisations?{query}");

            if (shouldMatch)
            {
                results.Should().ContainKey(id, $"\"{query}\" must return it");
                JsonNode
                    .DeepEquals(results[id], organisation)
                    .Should()
                    .BeTrue($"\"{query}\" must return what get returns");
            }
            else
            {
                results.Should().NotContainKey(id, $"\"{query}\" must not return it");
            }
        }
    }

    private static async Task<Dictionary<Guid, JsonObject>> Search(string path)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        return body["organisations"]!.AsArray().Select(o => o!.AsObject()).ToDictionary(o => o["id"]!.GetValue<Guid>());
    }

    private static async Task<JsonObject> GetOrganisation(Guid id)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode
            .Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!
            .AsObject();
    }

    private static async Task PutRegistration(Guid id, string key, string status, HttpStatusCode expected)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.PutAsync(
            $"/organisations/{id}/registrations/{key}",
            Json($$"""{ "status": "{{status}}" }"""),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(expected);
    }

    private static async Task DeleteRegistration(Guid id, string key)
    {
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.DeleteAsync(
            $"/organisations/{id}/registrations/{key}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
