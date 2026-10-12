using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

public class PutOrganisationConcurrencyTests : V1TestBase
{
    private const int ParallelRequests = 10;

    // The API reads the organisation, adds the registration and writes it back only if no one else
    // wrote in between; a request that loses the race gets 500 and stores nothing. Whether a race
    // happens depends on timing, so the test asserts what must hold either way: no request's
    // registration is lost after a 200, and no 500 request's registration is stored.
    [Fact]
    [Trait("Scenario", "ORG-PUT-14")]
    public async Task PutOrganisation_WhenSeveralUpdatesAtOnce_StoresExactlyTheRegistrationsThatReturnedOk()
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full(type: "SMALL_PRODUCER", registrationYear: 2025));
        var years = Enumerable.Range(2030, ParallelRequests).ToList();
        using var client = CreateClient(TestClient.Developer);

        var statuses = await Task.WhenAll(
            years.Select(async year =>
            {
                using var response = await client.PutAsync(
                    $"/organisations/{id}",
                    Json(OrganisationPayloads.Full(type: "LARGE_PRODUCER", registrationYear: year)),
                    TestContext.Current.CancellationToken
                );
                return (Year: year, response.StatusCode);
            })
        );

        statuses
            .Select(s => s.StatusCode)
            .Should()
            .AllSatisfy(code => code.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError));
        var okYears = statuses.Where(s => s.StatusCode == HttpStatusCode.OK).Select(s => s.Year).ToList();
        // Optimistic concurrency always lets the first writer through, so an all-500 run is a regression.
        okYears.Should().NotBeEmpty();
        var expected = okYears.Select(year => ("LARGE_PRODUCER", year)).Append(("SMALL_PRODUCER", 2025));
        (await StoredRegistrations(client, id)).Should().BeEquivalentTo(expected);
    }

    private static async Task<List<(string Type, int RegistrationYear)>> StoredRegistrations(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/organisations/{id}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );

        return document
            .RootElement.GetProperty("registrations")
            .EnumerateArray()
            .Select(r => (r.GetProperty("type").GetString()!, r.GetProperty("registrationYear").GetInt32()))
            .ToList();
    }
}
