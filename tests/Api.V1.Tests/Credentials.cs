using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Defra.WasteOrganisations.Api.V1.Tests;

/// <summary>
/// Builds the Authorization header values a v1 client sends. Built here rather than taken from
/// tests/Testing, so a change made there for v2 cannot change what these tests send.
/// </summary>
public static class Credentials
{
    /// <summary>The claim the API reads the OAuth client from.</summary>
    private const string ClientIdClaim = "client_id";

    public static AuthenticationHeaderValue Basic(string clientId, string secret) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}")));

    /// <summary>
    /// A Bearer token signed with a random key: the API does not check the signature, only the
    /// <c>client_id</c> claim and the lifetime. Pass <c>null</c> to leave the claim out.
    /// </summary>
    public static AuthenticationHeaderValue Bearer(string? clientId, bool expired = false)
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var claims = clientId is null ? [] : new[] { new Claim(ClientIdClaim, clientId) };

        // An expired token must be outside the default five-minute clock skew, so it is
        // made to have expired an hour ago rather than just now.
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: "Local",
            claims: claims,
            notBefore: expired ? now.AddHours(-2) : now.AddMinutes(-1),
            expires: expired ? now.AddHours(-1) : now.AddMinutes(30),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        );

        return new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
}

/// <summary>A client configured in the Compose stack, with the header it authenticates with.</summary>
public sealed record TestClient(string Name, Func<AuthenticationHeaderValue> Authorization)
{
    /// <summary>API key client with read and write scope, from <c>src/Api/appsettings.Development.json</c>.</summary>
    public static TestClient Developer { get; } =
        new(nameof(Developer), () => Credentials.Basic("Developer", "developer-pwd"));

    /// <summary>OAuth client with read and write scope, from <c>src/Api/appsettings.Development.json</c>.</summary>
    public static TestClient IntegrationTest { get; } =
        new(nameof(IntegrationTest), () => Credentials.Bearer("IntegrationTest"));

    // The limited-scope clients below are configured in compose.yml rather than in the API's
    // settings, so the API's own configuration is untouched by this suite.

    /// <summary>API key client with read scope only, from <c>compose.yml</c>.</summary>
    public static TestClient ReadOnlyApiKey { get; } =
        new(nameof(ReadOnlyApiKey), () => Credentials.Basic("ReadOnlyApiKey", "read-only-pwd"));

    /// <summary>API key client with write scope only, from <c>compose.yml</c>.</summary>
    public static TestClient WriteOnlyApiKey { get; } =
        new(nameof(WriteOnlyApiKey), () => Credentials.Basic("WriteOnlyApiKey", "write-only-pwd"));

    /// <summary>OAuth client with read scope only, from <c>compose.yml</c>.</summary>
    public static TestClient ReadOnlyOAuth { get; } =
        new(nameof(ReadOnlyOAuth), () => Credentials.Bearer("ReadOnlyOAuth"));

    /// <summary>OAuth client with write scope only, from <c>compose.yml</c>.</summary>
    public static TestClient WriteOnlyOAuth { get; } =
        new(nameof(WriteOnlyOAuth), () => Credentials.Bearer("WriteOnlyOAuth"));

    /// <summary>
    /// Looks a client up by name, so theories can take the name as serialisable inline data
    /// and each case shows up on its own in test output.
    /// </summary>
    public static TestClient Named(string name) =>
        name switch
        {
            nameof(Developer) => Developer,
            nameof(IntegrationTest) => IntegrationTest,
            nameof(ReadOnlyApiKey) => ReadOnlyApiKey,
            nameof(WriteOnlyApiKey) => WriteOnlyApiKey,
            nameof(ReadOnlyOAuth) => ReadOnlyOAuth,
            nameof(WriteOnlyOAuth) => WriteOnlyOAuth,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such test client"),
        };
}
