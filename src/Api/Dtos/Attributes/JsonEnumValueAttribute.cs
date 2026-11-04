using System.ComponentModel.DataAnnotations;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Dtos.Attributes;

/// <summary>
/// Accepts a string that, once trimmed, is exactly one of the JSON values of <typeparamref name="T"/>.
/// A null passes, so that <see cref="RequiredAttribute"/> alone reports a missing value.
/// </summary>
/// <remarks>
/// Binding the member as a string with this check, rather than as the enum, makes an unknown value
/// a validation error under the member's name instead of an unreadable body. The comparison is
/// ordinal: <c>JsonStringEnumConverter</c> would also accept other casings and numbers.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class JsonEnumValueAttribute<T>()
    : ValidationAttribute(() => $"The {{0}} field must be one of {string.Join(", ", s_values)}.")
    where T : struct, Enum
{
    private static readonly string[] s_values = Enum.GetValues<T>().Select(v => v.ToJsonValue()).ToArray();

    public override bool IsValid(object? value) =>
        value is null || (value is string s && s_values.Contains(s.Trim(), StringComparer.Ordinal));
}
