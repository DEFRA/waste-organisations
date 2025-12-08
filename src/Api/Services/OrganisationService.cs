using System.Diagnostics.CodeAnalysis;
using Api.Data;
using Api.Dtos;
using Api.Extensions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Organisation = Api.Data.Entities.Organisation;

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

    public async Task<List<Organisation>> Search(
        List<RegistrationType> registrationTypes,
        List<int> registrationYears,
        List<RegistrationStatus> registrationStatuses,
        CancellationToken cancellationToken
    )
    {
        var registrationTypeStrings = registrationTypes.Select(x => x.ToJsonValue()).ToList();
        var registrationStatusStrings = registrationStatuses.Select(x => x.ToJsonValue()).ToList();

        var query = dbContext
            .Organisations.AsQueryable()
            .Where(x =>
                x.Registrations.Any(y =>
                    (registrationTypeStrings.Count == 0 || registrationTypeStrings.Contains(y.Type))
                    && (registrationYears.Count == 0 || registrationYears.Contains(y.RegistrationYear))
                    && (registrationStatusStrings.Count == 0 || registrationStatusStrings.Contains(y.Status))
                )
            );

        return await query.ToListAsync(cancellationToken);
    }
}
