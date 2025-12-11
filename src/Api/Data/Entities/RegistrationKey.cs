using Api.Dtos;
using Api.Extensions;

namespace Api.Data.Entities;

public record RegistrationKey(string Type, int RegistrationYear)
{
    public static RegistrationKey From(RegistrationType type, int registrationYear) =>
        new(type.ToJsonValue(), registrationYear);

    public override string ToString() => $"{Type}-{RegistrationYear}";
}
