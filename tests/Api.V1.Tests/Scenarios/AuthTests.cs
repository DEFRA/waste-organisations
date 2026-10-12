using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1.Tests.Scenarios;

/// <summary>
/// The authentication and scope matrix across all five organisation endpoints, for API key
/// (Basic) and OAuth (Bearer) callers. The per-endpoint auth tests check that a refused request
/// changes nothing; this class checks that every endpoint answers each kind of caller the same way.
/// </summary>
public class AuthTests : V1TestBase
{
    private const string ExistingKey = "SMALL_PRODUCER-2025";

    /// <summary>What a caller sends, and whether the endpoint should accept it.</summary>
    private enum Outcome
    {
        Unauthorized,
        Forbidden,
        Success,
    }

    /// <summary>
    /// Each kind of caller, by name, with the outcome it should get. "Right" and "wrong" scope
    /// are picked per endpoint, since read and write endpoints need opposite scopes.
    /// </summary>
    private static readonly (string Name, Outcome Outcome)[] Callers =
    [
        ("NoCredentials", Outcome.Unauthorized),
        ("ApiKeyWrongSecret", Outcome.Unauthorized),
        ("ApiKeyUnknownClient", Outcome.Unauthorized),
        ("ApiKeyWrongScope", Outcome.Forbidden),
        ("ApiKeyRightScope", Outcome.Success),
        ("ApiKeyFullScope", Outcome.Success),
        // A Bearer token is not checked against a secret: the API trusts any signature and reads
        // the client from `client_id`. A token it cannot place has no scopes, so it gets 403, not 401.
        ("OAuthUnknownClient", Outcome.Forbidden),
        ("OAuthMissingClientId", Outcome.Forbidden),
        ("OAuthWrongScope", Outcome.Forbidden),
        ("OAuthRightScope", Outcome.Success),
        ("OAuthFullScope", Outcome.Success),
        ("BearerNotAToken", Outcome.Unauthorized),
        ("BearerExpired", Outcome.Unauthorized),
    ];

    /// <summary>The five endpoints, the scope each needs, and the status it returns on success.</summary>
    private static readonly (string Name, bool NeedsWrite, HttpStatusCode Success)[] Endpoints =
    [
        ("SearchOrganisations", false, HttpStatusCode.OK),
        ("GetOrganisation", false, HttpStatusCode.OK),
        ("PutOrganisation", true, HttpStatusCode.OK),
        ("PutRegistration", true, HttpStatusCode.OK),
        ("DeleteRegistration", true, HttpStatusCode.NoContent),
    ];

    public static TheoryData<string, string, HttpStatusCode> Matrix()
    {
        var data = new TheoryData<string, string, HttpStatusCode>();

        foreach (var endpoint in Endpoints)
        {
            foreach (var caller in Callers)
            {
                var expected = caller.Outcome switch
                {
                    Outcome.Unauthorized => HttpStatusCode.Unauthorized,
                    Outcome.Forbidden => HttpStatusCode.Forbidden,
                    _ => endpoint.Success,
                };
                data.Add(endpoint.Name, caller.Name, expected);
            }
        }

        return data;
    }

    // Each case gets its own organisation holding the registration the write endpoints touch, so a
    // refusal cannot be a 404 in disguise and success always has the same status.
    [Theory]
    [Trait("Scenario", "X-AUTH-01")]
    [MemberData(nameof(Matrix))]
    public async Task Endpoint_ByCaller_ReturnsExpectedStatus(string endpoint, string caller, HttpStatusCode expected)
    {
        var id = await CreateOrganisation(OrganisationPayloads.Full());
        var needsWrite = Endpoints.Single(e => e.Name == endpoint).NeedsWrite;

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = Authorization(caller, needsWrite);

        using var response = await Send(client, endpoint, id);

        response.StatusCode.Should().Be(expected);
    }

    private static AuthenticationHeaderValue? Authorization(string caller, bool needsWrite) =>
        caller switch
        {
            "NoCredentials" => null,
            "ApiKeyWrongSecret" => Credentials.Basic("Developer", "not-the-developer-pwd"),
            "ApiKeyUnknownClient" => Credentials.Basic("NoSuchClient", "developer-pwd"),
            "ApiKeyWrongScope" => (needsWrite ? TestClient.ReadOnlyApiKey : TestClient.WriteOnlyApiKey).Authorization(),
            "ApiKeyRightScope" => (needsWrite ? TestClient.WriteOnlyApiKey : TestClient.ReadOnlyApiKey).Authorization(),
            "ApiKeyFullScope" => TestClient.Developer.Authorization(),
            "OAuthUnknownClient" => Credentials.Bearer("NoSuchClient"),
            "OAuthMissingClientId" => Credentials.Bearer(null),
            "OAuthWrongScope" => (needsWrite ? TestClient.ReadOnlyOAuth : TestClient.WriteOnlyOAuth).Authorization(),
            "OAuthRightScope" => (needsWrite ? TestClient.WriteOnlyOAuth : TestClient.ReadOnlyOAuth).Authorization(),
            "OAuthFullScope" => TestClient.IntegrationTest.Authorization(),
            "BearerNotAToken" => new AuthenticationHeaderValue("Bearer", "not-a-token"),
            "BearerExpired" => Credentials.Bearer("IntegrationTest", expired: true),
            _ => throw new ArgumentOutOfRangeException(nameof(caller), caller, "No such caller"),
        };

    private static Task<HttpResponseMessage> Send(HttpClient client, string endpoint, Guid id)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        return endpoint switch
        {
            "SearchOrganisations" => client.GetAsync("/organisations", cancellationToken),
            "GetOrganisation" => client.GetAsync($"/organisations/{id}", cancellationToken),
            "PutOrganisation" => client.PutAsync(
                $"/organisations/{id}",
                Json(OrganisationPayloads.Full()),
                cancellationToken
            ),
            "PutRegistration" => client.PutAsync(
                $"/organisations/{id}/registrations/{ExistingKey}",
                Json("""{ "status": "CANCELLED" }"""),
                cancellationToken
            ),
            "DeleteRegistration" => client.DeleteAsync(
                $"/organisations/{id}/registrations/{ExistingKey}",
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "No such endpoint"),
        };
    }
}
