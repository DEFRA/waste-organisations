using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

// ReSharper disable ConvertToExtensionBlock

namespace Defra.WasteOrganisations.Api.Mapping;

public static class OrganisationExtensions
{
    public static Data.Entities.Organisation ToEntity(this OrganisationRegistration organisationRegistration, Guid id)
    {
        var registration = organisationRegistration.Registration.ToEntity();

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
            Registrations = new() { { registration.Key, registration } },
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
                .Registrations.Values.Select(x => new Registration
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

    public static Data.Entities.Organisation Remove(
        this Data.Entities.Organisation organisation,
        Data.Entities.Registration registration
    )
    {
        var registrations = organisation.Registrations;

        registrations.Remove(registration.Key);

        return organisation with
        {
            Registrations = registrations,
        };
    }

    private static (Dictionary<Data.Entities.RegistrationKey, Data.Entities.Registration>, bool) Patch(
        this Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear,
        RegistrationStatus status
    )
    {
        var registrations = organisation.Registrations;
        var key = new Data.Entities.RegistrationKey(type, registrationYear);

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

        return (registrations, isAdded);
    }

    private static (Dictionary<Data.Entities.RegistrationKey, Data.Entities.Registration>, bool) Patch(
        this Data.Entities.Organisation organisation,
        Registration registration
    ) => organisation.Patch(registration.Type, registration.RegistrationYear, registration.Status);

    public static Data.Entities.Registration GetRegistration(
        this Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear
    ) => organisation.Registrations[new Data.Entities.RegistrationKey(type, registrationYear)];

    public static Data.Entities.Registration? FindRegistration(
        this Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear
    )
    {
        organisation.Registrations.TryGetValue(
            new Data.Entities.RegistrationKey(type, registrationYear),
            out var result
        );

        return result;
    }
}
