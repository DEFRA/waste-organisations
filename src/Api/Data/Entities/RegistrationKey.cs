using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Data.Entities;

public record RegistrationKey(string Type, int RegistrationYear)
{
    public RegistrationKey(RegistrationType type, int registrationYear)
        : this(type.ToJsonValue(), registrationYear) { }

    public override string ToString() => $"{Type}-{RegistrationYear}";
}
