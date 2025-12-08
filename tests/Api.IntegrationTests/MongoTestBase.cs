using Api.Data;
using Api.Dtos;
using MongoDB.Driver;

namespace Api.IntegrationTests;

public class MongoTestBase : IntegrationTestBase, IAsyncLifetime
{
    public required IMongoCollection<Organisation> Organisations { get; set; }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public async ValueTask InitializeAsync()
    {
        Organisations = GetMongoCollection<Organisation>();

        await Organisations.DeleteManyAsync(
            FilterDefinition<Organisation>.Empty,
            TestContext.Current.CancellationToken
        );
    }

    protected static IMongoDatabase GetMongoDatabase()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(5);

        return new MongoClient(settings).GetDatabase("waste-organisations");
    }

    private static IMongoCollection<T> GetMongoCollection<T>() => GetMongoDatabase().GetCollection<T>(typeof(T).Name);

    static MongoTestBase()
    {
        ServiceCollectionExtensions.RegisterConventions();
    }
}
