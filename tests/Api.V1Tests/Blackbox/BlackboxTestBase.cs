using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Testing;

namespace Defra.WasteOrganisations.Api.V1Tests.Blackbox;

public abstract class BlackboxTestBase
{
    private const string DefaultBaseUrl = "http://localhost:8080";
    private const string DeveloperClientId = "Developer";
    private const string DeveloperSecret = "developer-pwd";
    private const string OAuthClientId = "IntegrationTest";

    protected static Uri BaseUrl => new(Environment.GetEnvironmentVariable("V1_TESTS_BASE_URL") ?? DefaultBaseUrl);

    protected static HttpClient CreateClient() => new() { BaseAddress = BaseUrl };

    protected static HttpClient CreateApiKeyClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = DeveloperApiKeyHeader();
        return client;
    }

    protected static HttpClient CreateOAuthClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = IntegrationTestBearerHeader();
        return client;
    }

    protected static AuthenticationHeaderValue DeveloperApiKeyHeader()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{DeveloperClientId}:{DeveloperSecret}"));

        return new AuthenticationHeaderValue(BasicAuthenticationHandler.SchemeName, credentials);
    }

    protected static AuthenticationHeaderValue IntegrationTestBearerHeader()
    {
        var claims = new[] { new Claim(Claims.ClientId, OAuthClientId) };

        return new AuthenticationHeaderValue(JwtAuthenticationHandler.SchemeName, Jwt.GenerateJwt(claims));
    }

    protected static Guid NewOrganisationId() => Guid.NewGuid();
}
