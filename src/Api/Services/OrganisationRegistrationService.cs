using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Services;

public class OrganisationRegistrationService(TimeProvider timeProvider)
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

    public (Data.Entities.Organisation Organisation, bool IsAdded) Patch(
        Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear,
        RegistrationRequest registration
    )
    {
        var (registrations, isAdded) = Patch(organisation, type, registrationYear, registration.Status);

        return (organisation with { Registrations = registrations }, isAdded);
    }

    private (Data.Entities.Registration[] Registrations, bool IsAdded) Patch(
        Data.Entities.Organisation organisation,
        RegistrationType type,
        int registrationYear,
        RegistrationStatus status
    )
    {
        var registrations = organisation.RegistrationsAsDictionary();
        var key = new Data.Entities.RegistrationKey(type, registrationYear);
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var newRegistration = new Data.Entities.Registration
        {
            Type = type.ToJsonValue(),
            RegistrationYear = registrationYear,
            Status = status.ToJsonValue(),
            Created = utcNow,
            Updated = utcNow,
        };
        var isAdded = true;

        if (registrations.Remove(key, out var existingRegistration))
        {
            newRegistration = newRegistration with { Created = existingRegistration.Created };
            isAdded = false;
        }

        registrations.Add(key, newRegistration);

        return (registrations.Values.ToArray(), isAdded);
    }

    private Data.Entities.Registration[] Patch(Data.Entities.Organisation organisation, Registration registration) =>
        Patch(organisation, registration.Type, registration.RegistrationYear, registration.Status).Registrations;
}
