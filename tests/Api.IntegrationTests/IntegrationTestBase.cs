using System.Net.Http.Headers;
using System.Security.Claims;
using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.IntegrationTests;

[Trait("Category", "IntegrationTests")]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase
{
    protected static ServiceProvider BuildServiceProvider()
    {
        // The Postgres section of src/Api/appsettings.Development.json, which compose.yml matches
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Postgres:DefaultHost"] = "127.0.0.1",
                    ["Postgres:Port"] = "5432",
                    ["Postgres:DatabaseName"] = "waste_organisations",
                    ["Postgres:User"] = "postgres",
                    ["Postgres:Password"] = "postgres",
                    ["Postgres:UseIamAuthentication"] = "false",
                }
            )
            .Build();

        return new ServiceCollection().AddPostgres(configuration).BuildServiceProvider();
    }

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
