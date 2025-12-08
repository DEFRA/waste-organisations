using System.Net.Http.Headers;
using System.Security.Claims;
using Api.Authentication;
using Testing;

namespace Api.IntegrationTests;

[Trait("Category", "IntegrationTests")]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase
{
    protected static HttpClient CreateClient()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtAuthenticationHandler.SchemeName,
            // See compose.yml for configuration of IntegrationTest client
            GenerateJwt("IntegrationTest")
        );

        return client;
    }

    private static string GenerateJwt(string clientId)
    {
        var claims = new[] { new Claim(Claims.ClientId, clientId) };

        return Jwt.GenerateJwt(claims);
    }
}
