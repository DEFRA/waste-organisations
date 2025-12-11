using Api.Dtos;
using Api.Extensions;

namespace Api.Mapping;

public static class RegistrationExtensions
{
    public static Registration ToDto(this Data.Entities.Registration registration) =>
        new()
        {
            Status = registration.Status.FromJsonValue<RegistrationStatus>(),
            Type = registration.Type.FromJsonValue<RegistrationType>(),
            RegistrationYear = registration.RegistrationYear,
        };
}
