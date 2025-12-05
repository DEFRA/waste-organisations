using System.Net.Http.Headers;
using System.Security.Claims;
using Api.Authentication;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
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

    protected static IMongoDatabase GetMongoDatabase()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(5);

        return new MongoClient(settings).GetDatabase("waste-organisations");
    }

    protected static IMongoCollection<T> GetMongoCollection<T>()
    {
        var db = GetMongoDatabase();

        return db.GetCollection<T>(typeof(T).Name);
    }

    protected IntegrationTestBase()
    {
        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
        };

        ConventionRegistry.Register(nameof(conventionPack), conventionPack, _ => true);
    }
}
