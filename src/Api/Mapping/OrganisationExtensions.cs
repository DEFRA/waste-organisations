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

    public static Data.Entities.Organisation Patch(
        this Data.Entities.Organisation organisation,
        OrganisationRegistration organisationRegistration
    )
    {
        var (registrations, _) = organisation.Patch(organisationRegistration.Registration);

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
            Registrations = registrations,
        };
    }

    public static (Data.Entities.Organisation Organisation, bool IsAdded) Patch(
        this Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear,
        RegistrationRequest registration
    )
    {
        var (registrations, isAdded) = organisation.Patch(type, registrationYear, registration.Status);

        return (organisation with { Registrations = registrations }, isAdded);
    }

    private static (Data.Entities.Registration[], bool) Patch(
        this Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear,
        RegistrationStatus status
    )
    {
        var registrations = organisation.Registrations.ToDictionary(
            x => new Data.Entities.RegistrationKey(x.Type, x.RegistrationYear),
            x => x
        );
        var key = Data.Entities.RegistrationKey.From(type, registrationYear);

        var isAdded = !registrations.Remove(key);
        registrations.Add(
            key,
            new Data.Entities.Registration
            {
                Type = type.ToJsonValue(),
                RegistrationYear = registrationYear,
                Status = status.ToJsonValue(),
            }
        );

        return (registrations.Values.ToArray(), isAdded);
    }

    private static (Data.Entities.Registration[], bool) Patch(
        this Data.Entities.Organisation organisation,
        Registration registration
    ) => organisation.Patch(registration.Type, registration.RegistrationYear, registration.Status);

    public static Data.Entities.Registration GetRegistration(
        this Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear
    ) =>
        organisation.Registrations.Single(x =>
            new Data.Entities.RegistrationKey(x.Type, x.RegistrationYear)
            == Data.Entities.RegistrationKey.From(type, registrationYear)
        );
}
