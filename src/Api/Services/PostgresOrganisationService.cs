using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.Services;

/// <summary>
/// Stores and reads v2 organisations through the request's <see cref="PostgresDbContext"/>
/// (spec 0005, Persistence). On a create, the reference rows are read untracked and attached on
/// save, so this service sets every navigation the response needs itself rather than relying on
/// what the context tracks.
/// </summary>
public class PostgresOrganisationService(PostgresDbContext context, ILogger<PostgresOrganisationService> logger)
    : IPostgresOrganisationService
{
    private const string AddressValuesIndex = "ux_address_values";

    public async Task<Guid> OrganisationTypeIdAsync(string code, CancellationToken cancellationToken) =>
        await context
            .OrganisationTypes.AsNoTracking()
            .Where(type => type.Code == code)
            .Select(type => (Guid?)type.Id)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new InvalidOperationException($"No organisation_type row has the code '{code}'.");

    public async Task<IReadOnlyDictionary<AddressType, Guid>> UsageTypeIdsAsync(CancellationToken cancellationToken)
    {
        var usageTypes = await LoadUsageTypesAsync(cancellationToken);

        return Enum.GetValues<AddressType>()
            .ToDictionary(
                kind => kind,
                kind =>
                    usageTypes.SingleOrDefault(usageType => usageType.Name == kind.ToJsonValue())?.Id
                    ?? throw new InvalidOperationException(
                        $"No address_usagetype row has the name '{kind.ToJsonValue()}'."
                    )
            );
    }

    /// <summary>
    /// Finds each distinct address of the organisation in the <c>address</c> table and links the
    /// organisation to the stored row, or inserts the address when there is none; then saves once,
    /// so everything is stored or nothing is. Two requests inserting the same new address at the
    /// same time both miss the look-up and the later save fails on <c>ux_address_values</c>; that
    /// save is rolled back and run once more with a fresh look-up, which then finds the row, and
    /// with its timestamps unset, so they are stamped afresh. A second failure, or any other
    /// failure, is thrown.
    /// </summary>
    /// <remarks>
    /// No explicit transaction is opened: <c>EnableRetryOnFailure</c> is on, and its execution
    /// strategy rejects a transaction started outside it. The single save is the transaction.
    /// </remarks>
    public async Task<Organisation> CreateAsync(Organisation organisation, CancellationToken cancellationToken)
    {
        // Each link's candidate, as the mapping built it. An attempt re-points the links at stored
        // rows, so the candidates are kept apart to look them up again on the retry
        var links = organisation.Addresses.Select(link => (Link: link, Candidate: link.Address)).ToList();

        var organisationType = organisation.OrganisationTypeId is { } organisationTypeId
            ? await FindOrganisationTypeAsync(organisationTypeId, cancellationToken)
            : null;
        if (organisationType is null)
            throw new InvalidOperationException(
                $"No organisation_type row has the id '{organisation.OrganisationTypeId}'."
            );

        var usageTypes = (await LoadUsageTypesAsync(cancellationToken)).ToDictionary(usageType => usageType.Id);

        try
        {
            await SaveAsync(organisation, organisationType, usageTypes, links, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsAddressValuesViolation(exception))
        {
            logger.LogInformation(
                exception,
                "An address of organisation '{OrganisationId}' was stored by a concurrent request; retrying once",
                organisation.Id
            );

            // Forgets the failed attempt: its organisation, links and new addresses were Added and
            // would be inserted again, and a row found then is looked up afresh
            context.ChangeTracker.Clear();

            // TimestampInterceptor sets ValidFrom only when it is unset but CreatedAt and UpdatedAt
            // always, so the failed attempt's ValidFrom would outlive it and the three would differ;
            // unset, all three are the retried save's instant
            organisation.ValidFrom = default;
            foreach (var (link, _) in links)
                link.ValidFrom = default;

            await SaveAsync(organisation, organisationType, usageTypes, links, cancellationToken);
        }

        logger.LogInformation("Created organisation with id '{OrganisationId}'", organisation.Id);

        return organisation;
    }

    public Task<Organisation?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OrganisationWithCurrentAddresses(id).SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The organisation with the given id, untracked, with its organisation type and its current
    /// links. EF Core merges the two identical filtered includes into one filtered join.
    /// </summary>
    protected IQueryable<Organisation> OrganisationWithCurrentAddresses(Guid id) =>
        context
            .Organisations.AsNoTracking()
            .Where(organisation => organisation.Id == id)
            .Include(organisation => organisation.OrganisationType)
            .Include(organisation => organisation.Addresses.Where(link => link.ValidTo == null))
                .ThenInclude(link => link.Address)
            .Include(organisation => organisation.Addresses.Where(link => link.ValidTo == null))
                .ThenInclude(link => link.UsageType);

    /// <summary>The <c>organisation_type</c> row with the given id, untracked.</summary>
    protected virtual Task<OrganisationType?> FindOrganisationTypeAsync(Guid id, CancellationToken cancellationToken) =>
        context.OrganisationTypes.AsNoTracking().SingleOrDefaultAsync(type => type.Id == id, cancellationToken);

    /// <summary>Every <c>address_usagetype</c> row, untracked: five rows, not cached.</summary>
    protected virtual Task<List<AddressUsageType>> LoadUsageTypesAsync(CancellationToken cancellationToken) =>
        context.AddressUsageTypes.AsNoTracking().ToListAsync(cancellationToken);

    /// <summary>
    /// The stored <c>address</c> row holding the candidate's nine parts, untracked, or null. EF
    /// Core turns a null part into <c>IS NULL</c>, so <c>ux_address_values</c> serves the query;
    /// that index also means there is at most one such row.
    /// </summary>
    protected virtual Task<Address?> FindAddressAsync(Address candidate, CancellationToken cancellationToken) =>
        context
            .Addresses.AsNoTracking()
            .FirstOrDefaultAsync(
                address =>
                    address.SubBuildingName == candidate.SubBuildingName
                    && address.BuildingName == candidate.BuildingName
                    && address.BuildingNumber == candidate.BuildingNumber
                    && address.Street == candidate.Street
                    && address.Locality == candidate.Locality
                    && address.Region == candidate.Region
                    && address.PostalCode == candidate.PostalCode
                    && address.PoBox == candidate.PoBox
                    && address.CountryIso == candidate.CountryIso,
                cancellationToken
            );

    private async Task SaveAsync(
        Organisation organisation,
        OrganisationType organisationType,
        Dictionary<Guid, AddressUsageType> usageTypes,
        List<(OrganisationAddress Link, Address Candidate)> links,
        CancellationToken cancellationToken
    )
    {
        // Rows that already exist are attached before the organisation is added, so Add leaves
        // them Unchanged instead of inserting them
        context.Attach(organisationType);
        foreach (var usageType in usageTypes.Values)
            context.Attach(usageType);

        organisation.OrganisationType = organisationType;

        // Distinct candidates differ in at least one of the nine parts, so no two find the same row
        var addresses = new Dictionary<Address, Address>(ReferenceEqualityComparer.Instance);

        foreach (var (link, candidate) in links)
        {
            if (!addresses.TryGetValue(candidate, out var address))
            {
                var stored = await FindAddressAsync(candidate, cancellationToken);
                if (stored is not null)
                    context.Attach(stored);

                address = stored ?? candidate;
                addresses.Add(candidate, address);
            }

            // Through the navigation, not only the foreign key: a link still holding the candidate
            // would make Add insert it, and the save would fail on ux_address_values
            link.Address = address;
            link.AddressId = address.Id;
            link.UsageType = usageTypes.TryGetValue(link.UsageTypeId, out var usageType)
                ? usageType
                : throw new InvalidOperationException($"No address_usagetype row has the id '{link.UsageTypeId}'.");
        }

        context.Organisations.Add(organisation);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsAddressValuesViolation(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: AddressValuesIndex };
}
