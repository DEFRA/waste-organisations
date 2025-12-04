using Api.Dtos;

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
            // should this be the same string?
            BusinessCountry = organisationRegistration.BusinessCountry?.ToString(),
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
                    // should this be the same string?
                    Status = organisationRegistration.Registration.Status.ToString(),
                    Type = organisationRegistration.Registration.Type.ToString(),
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
                businessCountry = Enum.Parse<BusinessCountry>(organisation.BusinessCountry);

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
                        Status = Enum.Parse<RegistrationStatus>(x.Status),
                        Type = Enum.Parse<RegistrationType>(x.Type),
                        RegistrationYear = x.RegistrationYear,
                    })
                    .ToArray(),
            };
        }

        public Data.Entities.Organisation Patch(OrganisationRegistration organisationRegistration)
        {
            var registrations = organisation.Registrations.ToDictionary(x => $"{x.Type}-{x.RegistrationYear}", x => x);
            var key =
                $"{organisationRegistration.Registration.Type.ToString()}-{organisationRegistration.Registration.RegistrationYear}";

            registrations.Remove(key);
            registrations.Add(
                key,
                new Data.Entities.Registration
                {
                    Type = organisationRegistration.Registration.Type.ToString(),
                    RegistrationYear = organisationRegistration.Registration.RegistrationYear,
                    Status = organisationRegistration.Registration.Status.ToString(),
                }
            );

            return organisation with
            {
                Name = organisationRegistration.Name,
                TradingName = organisationRegistration.TradingName,
                BusinessCountry = organisationRegistration.BusinessCountry?.ToString(),
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
