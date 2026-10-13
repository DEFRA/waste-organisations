using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests;

/// <summary>
/// Base for every v1 test. Tests talk to the running Docker Compose stack over HTTP only,
/// so the suite checks the contract a client sees rather than the API's own types.
/// </summary>
[Trait("Category", "V1Tests")]
[Collection("V1 Tests")]
public abstract class V1TestBase
{
    /// <summary>Overrides the API address, for example to run the suite against another host.</summary>
    public const string BaseAddressVariable = "V1_TESTS_BASE_ADDRESS";

    private const string DefaultBaseAddress = "http://localhost:8080";

    // The API truncates timestamps to the millisecond, so a second request made without
    // a pause could get the same `updated` value and hide whether it advanced.
    private static readonly TimeSpan WaitDuration = TimeSpan.FromMilliseconds(100);

    protected static Uri BaseAddress { get; } =
        new(Environment.GetEnvironmentVariable(BaseAddressVariable) ?? DefaultBaseAddress);

    /// <summary>Creates a client with no credentials; tests add the ones they need.</summary>
    protected static HttpClient CreateClient() => new() { BaseAddress = BaseAddress };

    /// <summary>Creates a client that sends the given client's credentials with every request.</summary>
    protected static HttpClient CreateClient(TestClient testClient)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = testClient.Authorization();
        return client;
    }

    /// <summary>Pause between two requests whose timestamps are compared.</summary>
    protected static Task Wait() => Task.Delay(WaitDuration, TestContext.Current.CancellationToken);

    /// <summary>
    /// Asserts that the body a PUT returned is the organisation a later GET returns, so a stale or
    /// incomplete write response cannot pass while storage is correct.
    /// </summary>
    protected static void AssertPutBodyMatchesGet(string putBody, string getBody) =>
        JsonNode
            .DeepEquals(JsonNode.Parse(putBody), JsonNode.Parse(getBody))
            .Should()
            .BeTrue(
                $"the PUT response must match the stored organisation.{Environment.NewLine}PUT: {putBody}{Environment.NewLine}GET: {getBody}"
            );

    /// <summary>Wraps a raw JSON body as <c>application/json</c>.</summary>
    protected static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    /// <summary>
    /// Creates an organisation with a new id through the API, as a full-scope client, so each
    /// test owns its data and never relies on what another test left behind.
    /// </summary>
    protected static async Task<Guid> CreateOrganisation(string body)
    {
        var id = Guid.NewGuid();
        using var client = CreateClient(TestClient.Developer);

        using var response = await client.PutAsync(
            $"/organisations/{id}",
            Json(body),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return id;
    }
}
