using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

// ReSharper disable ConvertToExtensionBlock

namespace Defra.WasteOrganisations.Api.Mapping;

/// <summary>
/// Maps a v2 create request to the Postgres entities, and the stored entities to the v2 response.
/// </summary>
public static class CreateOrganisationExtensions
{
    /// <summary>
    /// Builds the organisation to store from a validated request. Every string is trimmed,
    /// <c>companiesHouseNumber</c> and <c>countryIso</c> are upper-cased, and a blank optional part
    /// becomes null. Items holding the same nine normalised address parts share one
    /// <see cref="Address"/>, since an address is stored once (spec 0005, Shared addresses); each
    /// kind of each item becomes one <see cref="OrganisationAddress"/>, in request order, holding
    /// the <see cref="Address"/> navigation so the service can re-point it at a stored row.
    /// </summary>
    /// <param name="request">The organisation, already validated.</param>
    /// <param name="organisationTypeId">The id of the <c>organisation_type</c> row of the request's code.</param>
    /// <param name="usageTypeIds">The id of the <c>address_usagetype</c> row of every kind.</param>
    public static Organisation ToEntity(
        this OrganisationRequest request,
        Guid organisationTypeId,
        IReadOnlyDictionary<AddressType, Guid> usageTypeIds
    )
    {
        var organisation = new Organisation
        {
            Id = Guid.CreateVersion7(),
            Name = Normalise(request.Name),
            TradingName = Normalise(request.TradingName),
            CompaniesHouseNumber = Normalise(request.CompaniesHouseNumber)?.ToUpperInvariant(),
            RegulatorNation = Normalise(request.RegulatorNation)!,
            OrganisationTypeId = organisationTypeId,
        };

        var addressesByParts = new Dictionary<AddressParts, Address>();

        foreach (var item in request.Addresses ?? [])
        {
            var candidate = item.ToEntity();
            var parts = AddressParts.Of(candidate);

            if (!addressesByParts.TryGetValue(parts, out var address))
            {
                address = candidate;
                addressesByParts.Add(parts, address);
            }

            foreach (var kind in item.AddressTypes ?? [])
            {
                if (!usageTypeIds.TryGetValue(kind, out var usageTypeId))
                    throw new InvalidOperationException($"No address_usagetype id was given for {kind.ToJsonValue()}.");

                organisation.Addresses.Add(
                    new OrganisationAddress
                    {
                        Id = Guid.CreateVersion7(),
                        OrganisationId = organisation.Id,
                        Organisation = organisation,
                        AddressId = address.Id,
                        Address = address,
                        UsageTypeId = usageTypeId,
                    }
                );
            }
        }

        return organisation;
    }

    /// <summary>
    /// Builds the response from the stored organisation: one item per distinct
    /// <see cref="Address"/>, in order of first appearance among the organisation's
    /// <see cref="OrganisationAddress"/> rows, each with the kinds of its rows in that order.
    /// Needs the <see cref="Organisation.OrganisationType"/> and every
    /// <see cref="OrganisationAddress.UsageType"/> navigation set, which EF Core does when those
    /// rows are tracked by the context that saved the organisation.
    /// </summary>
    public static CreateOrganisationResponse ToResponse(this Organisation organisation)
    {
        var organisationType =
            organisation.OrganisationType
            ?? throw new InvalidOperationException("The organisation's OrganisationType is not loaded.");

        var addresses = organisation
            .Addresses.GroupBy(link => link.Address)
            .Select(group =>
                group.Key.ToResponse(group.Select(link => link.UsageType.Name.FromJsonValue<AddressType>()))
            )
            .ToArray();

        return new CreateOrganisationResponse
        {
            Organisation = new OrganisationResponse
            {
                Id = organisation.Id,
                Name = organisation.Name,
                TradingName = organisation.TradingName,
                CompaniesHouseNumber = organisation.CompaniesHouseNumber,
                RegulatorNation = organisation.RegulatorNation.FromJsonValue<RegulatorNation>(),
                OrganisationType = organisationType.Code,
                ValidFrom = AsUtc(organisation.ValidFrom),
                ValidTo = organisation.ValidTo is { } validTo ? AsUtc(validTo) : null,
                CreatedAt = AsUtc(organisation.CreatedAt),
                UpdatedAt = AsUtc(organisation.UpdatedAt),
                Addresses = addresses,
            },
        };
    }

    private static Address ToEntity(this AddressRequest item) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SubBuildingName = Normalise(item.SubBuildingName),
            BuildingName = Normalise(item.BuildingName),
            BuildingNumber = Normalise(item.BuildingNumber),
            Street = Normalise(item.Street),
            PoBox = Normalise(item.PoBox),
            Locality = Normalise(item.Locality),
            Region = Normalise(item.Region),
            PostalCode = Normalise(item.PostalCode),
            CountryIso = Normalise(item.CountryIso)?.ToUpperInvariant(),
        };

    private static AddressResponse ToResponse(this Address address, IEnumerable<AddressType> addressTypes) =>
        new()
        {
            AddressTypes = addressTypes.ToArray(),
            AddressLine = AddressLine.Build(address),
            SubBuildingName = address.SubBuildingName,
            BuildingName = address.BuildingName,
            BuildingNumber = address.BuildingNumber,
            Street = address.Street,
            PoBox = address.PoBox,
            Locality = address.Locality,
            Region = address.Region,
            PostalCode = address.PostalCode,
            CountryIso = address.CountryIso,
        };

    private static string? Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Npgsql reads timestamptz as UTC, and TimestampInterceptor writes UTC; the kind is set
    // explicitly so an unspecified kind is never taken as local time
    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>
    /// The nine parts that make two addresses the same, in the order the DTOs list them: compared
    /// exactly (ordinal), nulls equal, as <c>ux_address_values</c> compares them.
    /// </summary>
    private readonly record struct AddressParts(
        string? SubBuildingName,
        string? BuildingName,
        string? BuildingNumber,
        string? Street,
        string? PoBox,
        string? Locality,
        string? Region,
        string? PostalCode,
        string? CountryIso
    )
    {
        public static AddressParts Of(Address address) =>
            new(
                address.SubBuildingName,
                address.BuildingName,
                address.BuildingNumber,
                address.Street,
                address.PoBox,
                address.Locality,
                address.Region,
                address.PostalCode,
                address.CountryIso
            );
    }
}
