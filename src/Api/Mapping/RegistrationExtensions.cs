using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Mapping;

public static class RegistrationExtensions
{
    public static Data.Entities.Registration ToEntity(this Registration registration) =>
        new()
        {
            Status = registration.Status.ToJsonValue(),
            Type = registration.Type.ToJsonValue(),
            RegistrationYear = registration.RegistrationYear,
        };

    public static Registration ToDto(this Data.Entities.Registration registration) =>
        new()
        {
            Status = registration.Status.FromJsonValue<RegistrationStatus>(),
            Type = registration.Type.FromJsonValue<RegistrationType>(),
            RegistrationYear = registration.RegistrationYear,
        };
}
