using Api.Dtos;
using Api.Extensions;

namespace Api.Mapping;

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
            Registrations =
            [
                new Data.Entities.Registration
                {
                    Status = organisationRegistration.Registration.Status.ToJsonValue(),
                    Type = organisationRegistration.Registration.Type.ToJsonValue(),
                    RegistrationYear = organisationRegistration.Registration.RegistrationYear,
                },
            ],
        };
    }

    extension(Data.Entities.Organisation organisation)
    {
        public Organisation ToDto()
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
                Registrations = organisation
                    .Registrations.Select(x => new Registration
                    {
                        Status = x.Status.FromJsonValue<RegistrationStatus>(),
                        Type = x.Type.FromJsonValue<RegistrationType>(),
                        RegistrationYear = x.RegistrationYear,
                    })
                    .ToArray(),
            };
        }

        public Data.Entities.Organisation Patch(OrganisationRegistration organisationRegistration)
        {
            var registrations = organisation.Registrations.ToDictionary(x => $"{x.Type}-{x.RegistrationYear}", x => x);
            var type = organisationRegistration.Registration.Type.ToJsonValue();
            var key = $"{type}-{organisationRegistration.Registration.RegistrationYear}";

            registrations.Remove(key);
            registrations.Add(
                key,
                new Data.Entities.Registration
                {
                    Type = type,
                    RegistrationYear = organisationRegistration.Registration.RegistrationYear,
                    Status = organisationRegistration.Registration.Status.ToJsonValue(),
                }
            );

            return organisation with
            {
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
                Registrations = registrations.Values.ToArray(),
            };
        }
    }
}
