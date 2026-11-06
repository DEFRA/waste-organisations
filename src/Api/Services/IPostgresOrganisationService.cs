using Defra.WasteOrganisations.Api.Dtos;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.Services;

/// <summary>
/// Stores v2 organisations in Postgres. Named after its store so it does not collide with the
/// Mongo-backed v1 <see cref="IOrganisationService"/>, which is not extended.
/// </summary>
public interface IPostgresOrganisationService
{
    /// <summary>
    /// The id of the <c>organisation_type</c> row with the given code. Throws when there is none:
    /// validation has already checked the code against <c>OrganisationTypeCodes.All</c>, so a
    /// missing row is drift between the list and the changelog (a <c>500</c>).
    /// </summary>
    Task<Guid> OrganisationTypeIdAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// The id of the <c>address_usagetype</c> row of every <see cref="AddressType"/>, looked up by
    /// its wire value. Throws when a kind has no row.
    /// </summary>
    Task<IReadOnlyDictionary<AddressType, Guid>> UsageTypeIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores the organisation, its links and every address the <c>address</c> table does not
    /// already hold, in one save. Returns the organisation with its organisation type, usage
    /// types and addresses set, as the v2 response needs them.
    /// </summary>
    Task<Organisation> CreateAsync(Organisation organisation, CancellationToken cancellationToken);
}
