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
    protected const string AcceptV2 = "application/vnd.defra.waste-organisations+json; version=2";

    protected static ServiceProvider BuildServiceProvider(TimeProvider? timeProvider = null)
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

        var services = new ServiceCollection();
        // Registered first, so the TryAdd in AddPostgres keeps it
        if (timeProvider is not null)
            services.AddSingleton(timeProvider);

        return services.AddPostgres(configuration).BuildServiceProvider();
    }

    // See compose.yml for configuration of IntegrationTest client, which has read and write
    protected static HttpClient CreateClient() => CreateClient("IntegrationTest");

    // A client named in compose.yml's Acl__Clients, or no Authorization header when null
    protected static HttpClient CreateClient(string? clientId)
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };

        if (clientId is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                JwtAuthenticationHandler.SchemeName,
                GenerateJwt(clientId)
            );

        return client;
    }

    // Unique per test and per run, so a test never meets an address row another stored
    protected static string NewSuffix() => Guid.NewGuid().ToString("N")[..12];

    private static string GenerateJwt(string clientId)
    {
        var claims = new[] { new Claim(Claims.ClientId, clientId) };

        return Jwt.GenerateJwt(claims);
    }
}
