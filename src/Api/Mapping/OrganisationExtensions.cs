using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

// ReSharper disable ConvertToExtensionBlock

namespace Defra.WasteOrganisations.Api.Mapping;

public static class OrganisationExtensions
{
    public static Data.Entities.Organisation ToEntity(this OrganisationRegistration organisationRegistration, Guid id)
    {
        return new Data.Entities.Organisation
        {
            Id = id,
            Name = organisationRegistration.Name,
            TradingName = organisationRegistration.TradingName,
            BusinessCountry = organisationRegistration.BusinessCountry?.ToJsonValue(),
            CompaniesHouseNumber = organisationRegistration.CompaniesHouseNumber,
            Address = new Data.Entities.Address
            {
                AddressLine1 = organisationRegistration.Address.AddressLine1,
                AddressLine2 = organisationRegistration.Address.AddressLine2,
                Town = organisationRegistration.Address.Town,
                County = organisationRegistration.Address.County,
                Country = organisationRegistration.Address.Country,
                Postcode = organisationRegistration.Address.Postcode,
            },
            Registrations = [organisationRegistration.Registration.ToEntity()],
        };
    }

    public static Organisation ToDto(this Data.Entities.Organisation organisation)
    {
        BusinessCountry? businessCountry = null;
        if (organisation.BusinessCountry is not null)
            businessCountry = organisation.BusinessCountry.FromJsonValue<BusinessCountry>();

        return new Organisation
        {
            Id = organisation.Id,
            Name = organisation.Name,
            TradingName = organisation.TradingName,
            BusinessCountry = businessCountry,
            CompaniesHouseNumber = organisation.CompaniesHouseNumber,
            Address = new Address
            {
                AddressLine1 = organisation.Address.AddressLine1,
                AddressLine2 = organisation.Address.AddressLine2,
                Town = organisation.Address.Town,
                County = organisation.Address.County,
                Country = organisation.Address.Country,
                Postcode = organisation.Address.Postcode,
            },
            Registrations = organisation.Registrations.Select(x => x.ToDto()).ToArray(),
        };
    }
}
