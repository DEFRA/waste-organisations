using Defra.WasteOrganisations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteOrganisations.Api.Data;

public interface IDbContext
{
    IMongoCollection<Organisation> Organisations { get; }
}
