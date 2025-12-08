using System.Diagnostics.CodeAnalysis;
using Api.Data.Entities;
using MongoDB.Driver;

namespace Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class MongoDbContext(IMongoDatabase database) : IDbContext
{
    public IMongoCollection<Organisation> Organisations { get; } =
        database.GetCollection<Organisation>(nameof(Organisation));
}
