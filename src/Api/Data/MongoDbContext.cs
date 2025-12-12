using System.Diagnostics.CodeAnalysis;
using Defra.WasteOrganisations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteOrganisations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class MongoDbContext(IMongoDatabase database) : IDbContext
{
    public IMongoCollection<Organisation> Organisations { get; } =
        database.GetCollection<Organisation>(nameof(Organisation));
}
