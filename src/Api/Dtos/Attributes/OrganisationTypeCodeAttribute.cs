using System.ComponentModel.DataAnnotations;
using Defra.WasteOrganisations.Api.Data.Postgres;

namespace Defra.WasteOrganisations.Api.Dtos.Attributes;

/// <summary>
/// Accepts a string that, once trimmed, is exactly one of the codes in
/// <see cref="OrganisationTypeCodes.All"/>. A null passes, so that <see cref="RequiredAttribute"/>
/// alone reports a missing value.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class OrganisationTypeCodeAttribute()
    : ValidationAttribute(() =>
        $"The {{0}} field must be one of {string.Join(", ", OrganisationTypeCodes.All.Select(c => c.Code))}."
    )
{
    public override bool IsValid(object? value) =>
        value is null
        || (
            value is string s
            && OrganisationTypeCodes.All.Any(c => string.Equals(c.Code, s.Trim(), StringComparison.Ordinal))
        );
}
