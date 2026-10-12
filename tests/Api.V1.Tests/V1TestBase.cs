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
}
