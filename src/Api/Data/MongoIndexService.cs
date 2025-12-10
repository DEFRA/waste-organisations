using System.Diagnostics.CodeAnalysis;
using Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class MongoIndexService(IMongoDatabase database, ILogger<MongoIndexService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateIndex(
            "Search",
            Builders<Organisation>
                .IndexKeys.Ascending(new StringFieldDefinition<Organisation>("registrations.type"))
                .Ascending(new StringFieldDefinition<Organisation>("registrations.registrationYear"))
                .Ascending(new StringFieldDefinition<Organisation>("registrations.status")),
            cancellationToken: cancellationToken
        );
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task CreateIndex<T>(
        string name,
        IndexKeysDefinition<T> keys,
        bool unique = false,
        CancellationToken cancellationToken = default
    )
    {
        var collectionName = typeof(T).Name;

        try
        {
            var collection = database.GetCollection<T>(collectionName);
            var requestedKeys = keys.Render(
                new RenderArgs<T>(collection.DocumentSerializer, collection.Settings.SerializerRegistry)
            );

            using (var cursor = await collection.Indexes.ListAsync(cancellationToken))
            {
                var existingIndexes = await cursor.ToListAsync(cancellationToken);
                var existingByName = existingIndexes.FirstOrDefault(i => i.TryGetValue("name", out var n) && n == name);

                if (existingByName is not null)
                {
                    var existingKeys = existingByName.GetValue("key", new BsonDocument()).AsBsonDocument;
                    var existingUnique = existingByName.TryGetValue("unique", out var u) && u.IsBoolean && u.AsBoolean;

                    if (!existingKeys.Equals(requestedKeys) || existingUnique != unique)
                    {
                        logger.LogInformation(
                            "Updating index {Name} on {Collection}: keys/options differ. Dropping and recreating.",
                            name,
                            collectionName
                        );

                        await DropIndex(name, collection, cancellationToken);
                    }
                    else
                    {
                        // Index already exists and is correct
                        return;
                    }
                }
            }

            var indexModel = new CreateIndexModel<T>(
                keys,
                new CreateIndexOptions
                {
                    Name = name,
                    Background = true,
                    Unique = unique,
                }
            );

            await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to Create index {Name} on {Collection}", name, collectionName);
        }
    }

    private async Task DropIndex<T>(string name, IMongoCollection<T> collection, CancellationToken cancellationToken)
    {
        try
        {
            await collection.Indexes.DropOneAsync(name, cancellationToken);
        }
        catch (MongoCommandException mongoCommandException)
        {
            logger.LogWarning(
                mongoCommandException,
                "Index {Name} was not dropped on {Collection}. It may not exist.",
                name,
                collection.CollectionNamespace.CollectionName
            );
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to drop index {Name} on {Collection}",
                name,
                collection.CollectionNamespace.CollectionName
            );
        }
    }
}
