using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

// ReSharper disable ConvertToExtensionBlock

namespace Defra.WasteOrganisations.Api.Mapping;

/// <summary>
/// Maps a stored organisation and its current addresses to the v2 <c>GET /organisations/{id}</c> response.
/// </summary>
public static class OrganisationDetailsExtensions
{
    /// <summary>
    /// Builds the response holding the organisation as its one item: one address item per distinct
    /// <see cref="OrganisationAddress.AddressId"/> among the organisation's
    /// <see cref="OrganisationAddress"/> rows, each with the kinds of its rows. Expects
    /// <see cref="Organisation.Addresses"/> to hold only the current links, with every
    /// <see cref="OrganisationAddress.Address"/> and <see cref="OrganisationAddress.UsageType"/>
    /// navigation loaded.
    /// </summary>
    public static GetOrganisationDetailsResponse ToDetailsResponse(this Organisation organisation)
    {
        // A no-tracking query gives each link its own Address instance, so links are grouped by
        // the id rather than by the reference
        var addresses = organisation
            .Addresses.GroupBy(link => link.AddressId)
            .Select(group =>
                group
                    .First()
                    .Address.ToDetailsResponse(group.Select(link => link.UsageType.Name.FromJsonValue<AddressType>()))
            )
            .ToArray();

        return new GetOrganisationDetailsResponse
        {
            Organisations =
            [
                new OrganisationDetailsResponse
                {
                    Id = organisation.Id,
                    Name = organisation.Name,
                    TradingName = organisation.TradingName,
                    CompaniesHouseNumber = organisation.CompaniesHouseNumber,
                    RegulatorNation = organisation.RegulatorNation.FromJsonValue<RegulatorNation>(),
                    OrganisationType = organisation.OrganisationType?.Code,
                    ValidTo = organisation.ValidTo is { } validTo ? AsUtc(validTo) : null,
                    Addresses = addresses,
                },
            ],
        };
    }

    private static AddressDetailsResponse ToDetailsResponse(
        this Address address,
        IEnumerable<AddressType> addressTypes
    ) =>
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

    // Npgsql reads timestamptz as UTC; the kind is set explicitly so an unspecified kind is never
    // taken as local time
    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
