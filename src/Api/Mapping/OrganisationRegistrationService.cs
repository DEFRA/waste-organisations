using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Mapping;

#pragma warning disable CS9113 // Parameter is unread.
public class OrganisationRegistrationService(TimeProvider timeProvider)
#pragma warning restore CS9113 // Parameter is unread.
{
    public Data.Entities.Organisation Patch(
        Data.Entities.Organisation organisation,
        OrganisationRegistration organisationRegistration
    )
    {
        var registrations = Patch(organisation, organisationRegistration.Registration);

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
        Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear,
        RegistrationRequest registration
    )
    {
        var (registrations, isAdded) = Patch(organisation, type, registrationYear, registration.Status);

        return (organisation with { Registrations = registrations }, isAdded);
    }

    public static Data.Entities.Organisation RemoveRegistration(
        Data.Entities.Organisation organisation,
        Data.Entities.Registration registration
    )
    {
        var registrations = organisation.RegistrationsAsDictionary();

        registrations.Remove(registration.Key);

        return organisation with
        {
            Registrations = registrations.Values.ToArray(),
        };
    }

    private static (Data.Entities.Registration[] Registrations, bool IsAdded) Patch(
        Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear,
        RegistrationStatus status
    )
    {
        var registrations = organisation.RegistrationsAsDictionary();
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

        return (registrations.Values.ToArray(), isAdded);
    }

    private static Data.Entities.Registration[] Patch(
        Data.Entities.Organisation organisation,
        Registration registration
    ) => Patch(organisation, registration.Type, registration.RegistrationYear, registration.Status).Registrations;

    public static Data.Entities.Registration GetRegistration(
        Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear
    ) => organisation.RegistrationsAsDictionary()[new Data.Entities.RegistrationKey(type, registrationYear)];

    public static Data.Entities.Registration? FindRegistration(
        Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear
    )
    {
        organisation
            .RegistrationsAsDictionary()
            .TryGetValue(new Data.Entities.RegistrationKey(type, registrationYear), out var result);

        return result;
    }
}
