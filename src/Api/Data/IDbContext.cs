using Api.Data.Entities;
using MongoDB.Driver;

namespace Api.Data;

public interface IDbContext
{
    IMongoCollection<Organisation> Organisations { get; }
}
