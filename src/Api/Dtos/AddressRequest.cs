using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteOrganisations.Api.Dtos.Validation;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// An address in the "multiple text inputs" format of the GOV.UK Design System pattern "Addresses",
/// with the country as a two-letter code, and the kinds of address it is.
/// </summary>
public record AddressRequest : IValidatableObject
{
    // The JSON member names, shared by the attributes and the validation rules so an error key or
    // message cannot name a member the wire does not have
    internal const string AddressTypesMember = "addressTypes";
    internal const string AddressLine1Member = "addressLine1";
    internal const string AddressLine2Member = "addressLine2";
    internal const string TownOrCityMember = "townOrCity";
    internal const string CountyMember = "county";
    internal const string PostcodeMember = "postcode";
    internal const string CountryIsoMember = "countryIso";

    /// <summary>
    /// Every kind of address this is, each at most once. One item may list all five kinds.
    /// </summary>
    [JsonPropertyName(AddressTypesMember)]
    [Required]
    [MinLength(1, ErrorMessage = "The {0} field must hold at least one item.")]
    public List<AddressType>? AddressTypes { get; init; }

    /// <summary>The first line of the address, such as the building and the street.</summary>
    [JsonPropertyName(AddressLine1Member)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    [Required]
    public string? AddressLine1 { get; init; }

    /// <summary>The second line of the address.</summary>
    [JsonPropertyName(AddressLine2Member)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? AddressLine2 { get; init; }

    /// <summary>The town or city.</summary>
    [JsonPropertyName(TownOrCityMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    [Required]
    public string? TownOrCity { get; init; }

    /// <summary>The county; for an address outside the UK, the state or province.</summary>
    [JsonPropertyName(CountyMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? County { get; init; }

    /// <summary>
    /// The postcode. Required when <c>countryIso</c> is <c>GB</c>: every character that is not a
    /// letter or a digit is removed, the rest is upper-cased and must be a full UK postcode, such as
    /// <c>AB1 2CD</c>, and it is stored as the outward code, one space and the inward code, so
    /// <c>ab1-2cd</c> is stored as <c>AB1 2CD</c>. The format only is checked, not that the postcode
    /// exists. Optional and not checked against a format otherwise, because some countries have no
    /// postcodes; upper-cased, with each run of whitespace inside it made one space.
    /// </summary>
    [JsonPropertyName(PostcodeMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? Postcode { get; init; }

    /// <summary>
    /// The country as an ISO 3166-1 alpha-2 code, upper-cased before it is checked. <c>GB</c> is
    /// the United Kingdom, Northern Ireland included.
    /// </summary>
    [JsonPropertyName(CountryIsoMember)]
    [Required]
    public string? CountryIso { get; init; }

    // Member names are the JSON property names, so the request validator can key each error by its
    // JSON path. `addressLine1`, `townOrCity` and `countryIso` blank or missing are left to [Required]
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var repeated = AddressTypes
            ?.GroupBy(t => t)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.ToJsonValue())
            .ToList();

        if (repeated is { Count: > 0 })
            yield return new ValidationResult(
                $"The {AddressTypesMember} field must not repeat a value: {string.Join(", ", repeated)}.",
                [AddressTypesMember]
            );

        var countryIso = CountryIso?.Trim().ToUpperInvariant();

        if (!string.IsNullOrEmpty(countryIso) && !IsCountryCode(countryIso))
            yield return new ValidationResult(
                $"The {CountryIsoMember} field must be an ISO 3166-1 alpha-2 code of two letters, such as GB.",
                [CountryIsoMember]
            );

        if (countryIso != "GB")
            yield break;

        if (UkPostcode.IsBlank(Postcode))
            yield return new ValidationResult(
                $"The {PostcodeMember} field is required when {CountryIsoMember} is GB.",
                [PostcodeMember]
            );
        else if (!UkPostcode.TryNormalise(Postcode, out _))
            yield return new ValidationResult(
                $"The {PostcodeMember} field must be a full UK postcode.",
                [PostcodeMember]
            );
    }

    // ^[A-Z]{2}$, checked without a regex
    private static bool IsCountryCode(string value) => value.Length == 2 && value.All(char.IsAsciiLetterUpper);
}
