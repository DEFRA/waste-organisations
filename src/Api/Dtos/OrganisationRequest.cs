using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteOrganisations.Api.Data.Postgres;
using Defra.WasteOrganisations.Api.Dtos.Attributes;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// An organisation to create. Every string is trimmed before it is validated and stored.
/// </summary>
public record OrganisationRequest : IValidatableObject
{
    // The JSON member names, shared by the attributes and the validation rules so an error key or
    // message cannot name a member the wire does not have
    internal const string NameMember = "name";
    internal const string TradingNameMember = "tradingName";
    internal const string CompaniesHouseNumberMember = "companiesHouseNumber";
    internal const string RegulatorNationMember = "regulatorNation";
    internal const string OrganisationTypeMember = "organisationType";
    internal const string AddressesMember = "addresses";

    /// <summary>
    /// The registered name. Required for a type registered at Companies House; optional otherwise.
    /// Must not be blank when present.
    /// </summary>
    [JsonPropertyName(NameMember)]
    public string? Name { get; init; }

    /// <summary>
    /// The name the organisation trades under. Required for a type not registered at Companies
    /// House; optional otherwise. Must not be blank when present.
    /// </summary>
    [JsonPropertyName(TradingNameMember)]
    public string? TradingName { get; init; }

    /// <summary>
    /// The Companies House number: two letters or digits followed by six digits, for example
    /// <c>12345678</c> or <c>SC123456</c>; upper-cased before it is checked. Required for a type
    /// registered at Companies House and must be absent for every other type.
    /// </summary>
    [JsonPropertyName(CompaniesHouseNumberMember)]
    public string? CompaniesHouseNumber { get; init; }

    /// <summary>
    /// The nation whose regulator the organisation deals with, which for an overseas organisation
    /// is not where it is based: <c>GB-ENG</c>, <c>GB-SCT</c>, <c>GB-WLS</c> or <c>GB-NIR</c>.
    /// </summary>
    [JsonPropertyName(RegulatorNationMember)]
    [Required]
    [JsonEnumValue<RegulatorNation>]
    public string? RegulatorNation { get; init; }

    /// <summary>
    /// The organisation type, one of the published codes in upper snake case, matched exactly. The
    /// type decides whether <c>name</c> or <c>tradingName</c> is required, whether
    /// <c>companiesHouseNumber</c> is required or must be absent, and whether a <c>REGISTERED</c> or
    /// a <c>PRINCIPAL</c> address is required.
    /// </summary>
    [JsonPropertyName(OrganisationTypeMember)]
    [Required]
    [OrganisationTypeCode]
    public string? OrganisationType { get; init; }

    /// <summary>
    /// One to five addresses, each listing in <c>addressTypes</c> every kind it serves as; a kind
    /// may appear in one item only. Two items holding the same address come back as one item
    /// carrying the kinds of both.
    /// </summary>
    [JsonPropertyName(AddressesMember)]
    [Required]
    [MinLength(1, ErrorMessage = "The {0} field must hold at least one item.")]
    [MaxLength(5, ErrorMessage = "The {0} field must hold at most five items.")]
    public List<AddressRequest>? Addresses { get; init; }

    // Member names are the JSON property names, relative to the organisation, so the request
    // validator can key each error by its JSON path. The type-dependent rules read the type's
    // RegisteredAtCompaniesHouse flag; an unknown or missing type is reported by its attributes
    // alone, since rules keyed on it would only add noise
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in ValidateNonBlank(Name, NameMember))
            yield return result;
        foreach (var result in ValidateNonBlank(TradingName, TradingNameMember))
            yield return result;

        foreach (var result in ValidateKindsAcrossAddresses())
            yield return result;

        var code = OrganisationType?.Trim();
        var type = OrganisationTypeCodes.All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.Ordinal));

        if (type is null)
            yield break;

        foreach (var result in ValidateByType(type))
            yield return result;
    }

    private static IEnumerable<ValidationResult> ValidateNonBlank(string? value, string member)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
            yield return new ValidationResult($"The {member} field must not be blank.", [member]);
    }

    private IEnumerable<ValidationResult> ValidateByType(OrganisationTypeCode type)
    {
        var nameMember = type.RegisteredAtCompaniesHouse ? NameMember : TradingNameMember;
        var nameValue = type.RegisteredAtCompaniesHouse ? Name : TradingName;

        // A blank value is already reported as blank
        if (nameValue is null)
            yield return new ValidationResult(
                $"The {nameMember} field is required for organisation type {type.Code}.",
                [nameMember]
            );

        if (type.RegisteredAtCompaniesHouse)
        {
            if (string.IsNullOrWhiteSpace(CompaniesHouseNumber))
                yield return new ValidationResult(
                    $"The {CompaniesHouseNumberMember} field is required for organisation type {type.Code}.",
                    [CompaniesHouseNumberMember]
                );
            else if (!IsCompaniesHouseNumber(CompaniesHouseNumber.Trim().ToUpperInvariant()))
                yield return new ValidationResult(
                    $"The {CompaniesHouseNumberMember} field must be two letters or digits followed by six digits, "
                        + "such as 12345678 or SC123456.",
                    [CompaniesHouseNumberMember]
                );
        }
        else if (CompaniesHouseNumber is not null)
        {
            yield return new ValidationResult(
                $"The {CompaniesHouseNumberMember} field must be absent for organisation type {type.Code}.",
                [CompaniesHouseNumberMember]
            );
        }

        // An empty or missing list is already reported by its attributes
        var requiredKind = type.RegisteredAtCompaniesHouse ? AddressType.Registered : AddressType.Principal;

        if (Addresses is { Count: > 0 } && !Addresses.Any(a => a?.AddressTypes?.Contains(requiredKind) == true))
            yield return new ValidationResult(
                $"The {AddressesMember} field must hold an address with the {requiredKind.ToJsonValue()} type "
                    + $"for organisation type {type.Code}.",
                [AddressesMember]
            );
    }

    // Each kind may be carried by one item only, so the organisation has at most one current
    // address per kind. A kind repeated within one item is that item's own error
    private IEnumerable<ValidationResult> ValidateKindsAcrossAddresses()
    {
        if (Addresses is null)
            yield break;

        var firstIndexByKind = new Dictionary<AddressType, int>();

        for (var index = 0; index < Addresses.Count; index++)
        {
            foreach (var kind in Addresses[index]?.AddressTypes?.Distinct() ?? [])
            {
                if (firstIndexByKind.TryGetValue(kind, out var firstIndex))
                    yield return new ValidationResult(
                        $"The {AddressRequest.AddressTypesMember} field must not repeat {kind.ToJsonValue()}, "
                            + $"which {AddressesMember}[{firstIndex}] already carries.",
                        [$"{AddressesMember}[{index}].{AddressRequest.AddressTypesMember}"]
                    );
                else
                    firstIndexByKind[kind] = index;
            }
        }
    }

    // ^[A-Z0-9]{2}[0-9]{6}$, checked without a regex
    private static bool IsCompaniesHouseNumber(string value) =>
        value.Length == 8
        && value[..2].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c))
        && value[2..].All(char.IsAsciiDigit);
}
