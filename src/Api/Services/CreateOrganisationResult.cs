using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.Services;

/// <summary>
/// The outcome of <see cref="IPostgresOrganisationService.CreateAsync"/>: either the stored
/// organisation, or the id of the organisation that already holds its Companies House number
/// (spec 0008, Enforcement).
/// </summary>
public abstract record CreateOrganisationResult
{
    // Only this assembly can derive, so the two records below are the whole set
    private protected CreateOrganisationResult() { }
}

public sealed record OrganisationCreated(Organisation Organisation) : CreateOrganisationResult;

public sealed record OrganisationAlreadyExists(Guid ExistingId) : CreateOrganisationResult;
