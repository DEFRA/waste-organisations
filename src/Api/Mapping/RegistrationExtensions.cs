using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Mapping;

public static class RegistrationExtensions
{
    public static Data.Entities.Registration ToEntity(this Registration registration, TimeProvider timeProvider)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();

        return new Data.Entities.Registration
        {
            Status = registration.Status.ToJsonValue(),
            Type = registration.Type.ToJsonValue(),
            RegistrationYear = registration.RegistrationYear,
            Created = utcNow,
            Updated = utcNow,
        };
    }

    public static RegistrationResponse ToDto(this Data.Entities.Registration registration, DateTime dateTimeFallback) =>
        new()
        {
            Status = registration.Status.FromJsonValue<RegistrationStatus>(),
            Type = registration.Type.FromJsonValue<RegistrationType>(),
            RegistrationYear = registration.RegistrationYear,
            Created = registration.Created ?? dateTimeFallback,
            Updated = registration.Updated ?? dateTimeFallback,
        };
}
