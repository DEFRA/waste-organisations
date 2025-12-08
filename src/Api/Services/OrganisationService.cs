using System.Diagnostics.CodeAnalysis;
using Api.Data;
using Api.Data.Entities;
using MongoDB.Driver;

namespace Api.Services;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class OrganisationService(IDbContext dbContext) : IOrganisationService
{
    public async Task<Organisation?> Get(Guid id, CancellationToken cancellationToken) =>
        await dbContext
            .Organisations.Find(Builders<Organisation>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

    public async Task<Organisation> Create(Organisation organisation, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        organisation = organisation with { Version = 1, Created = utcNow, Updated = utcNow };

        await dbContext.Organisations.InsertOneAsync(organisation, cancellationToken: cancellationToken);

        return organisation;
    }

    public async Task<Organisation> Update(Organisation organisation, CancellationToken cancellationToken)
    {
        var filter = Builders<Organisation>.Filter.And(
            Builders<Organisation>.Filter.Eq(x => x.Id, organisation.Id),
            Builders<Organisation>.Filter.Eq(x => x.Version, organisation.Version)
        );

        organisation = organisation with { Version = organisation.Version + 1, Updated = DateTime.UtcNow };

        var replaceOneResult = await dbContext.Organisations.ReplaceOneAsync(
            filter,
            organisation,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken: cancellationToken
        );

        return replaceOneResult.ModifiedCount == 0
            ? throw new ConcurrencyException("Concurrency issue on write, organisation was not updated")
            : organisation;
    }
}
