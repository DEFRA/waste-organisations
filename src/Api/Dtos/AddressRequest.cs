using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// An address in the Defra standard address model, with the country as a two-letter code, and
/// the kinds of address it is. At least one of <c>subBuildingName</c>, <c>buildingName</c>,
/// <c>buildingNumber</c> and <c>poBox</c> must be given, so the premises can be identified.
/// </summary>
public record AddressRequest : IValidatableObject
{
    // The JSON member names, shared by the attributes and the validation rules so an error key or
    // message cannot name a member the wire does not have
    internal const string AddressTypesMember = "addressTypes";
    internal const string SubBuildingNameMember = "subBuildingName";
    internal const string BuildingNameMember = "buildingName";
    internal const string BuildingNumberMember = "buildingNumber";
    internal const string StreetMember = "street";
    internal const string PoBoxMember = "poBox";
    internal const string LocalityMember = "locality";
    internal const string RegionMember = "region";
    internal const string PostalCodeMember = "postalCode";
    internal const string CountryIsoMember = "countryIso";

    /// <summary>
    /// Every kind of address this is, each at most once. One item may list all five kinds.
    /// </summary>
    [JsonPropertyName(AddressTypesMember)]
    [Required]
    [MinLength(1, ErrorMessage = "The {0} field must hold at least one item.")]
    public List<AddressType>? AddressTypes { get; init; }

    /// <summary>A flat or unit within the building.</summary>
    [JsonPropertyName(SubBuildingNameMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? SubBuildingName { get; init; }

    /// <summary>The name of the building.</summary>
    [JsonPropertyName(BuildingNameMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? BuildingName { get; init; }

    /// <summary>The building number, which may hold letters or a range, such as <c>12A</c> or <c>3-5</c>.</summary>
    [JsonPropertyName(BuildingNumberMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? BuildingNumber { get; init; }

    /// <summary>The street.</summary>
    [JsonPropertyName(StreetMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    [Required]
    public string? Street { get; init; }

    /// <summary>A PO box.</summary>
    [JsonPropertyName(PoBoxMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? PoBox { get; init; }

    /// <summary>The town or city.</summary>
    [JsonPropertyName(LocalityMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    [Required]
    public string? Locality { get; init; }

    /// <summary>The county, state or province.</summary>
    [JsonPropertyName(RegionMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? Region { get; init; }

    /// <summary>
    /// The postcode. Required when <c>countryIso</c> is <c>GB</c>; optional otherwise, because some
    /// countries have no postcodes. Not checked against a format. Upper-cased, with each run of
    /// whitespace inside it made one space, before it is stored and compared with stored addresses.
    /// </summary>
    [JsonPropertyName(PostalCodeMember)]
    [MaxLength(OrganisationRequest.MaxTextLength, ErrorMessage = OrganisationRequest.MaxTextLengthMessage)]
    public string? PostalCode { get; init; }

    /// <summary>
    /// The country as an ISO 3166-1 alpha-2 code, upper-cased before it is checked. <c>GB</c> is
    /// the United Kingdom, Northern Ireland included.
    /// </summary>
    [JsonPropertyName(CountryIsoMember)]
    [Required]
    public string? CountryIso { get; init; }

    // Member names are the JSON property names, so the request validator can key each error by its
    // JSON path. `street`, `locality` and `countryIso` blank or missing are left to [Required]
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

        // A blank postcode on a UK address is missing, not absent; on any other address a blank
        // optional part is stored as null
        if (countryIso == "GB" && string.IsNullOrWhiteSpace(PostalCode))
            yield return new ValidationResult(
                $"The {PostalCodeMember} field is required when {CountryIsoMember} is GB.",
                [PostalCodeMember]
            );

        // Mirrors ck_address_premises, so a valid request cannot fail that constraint. No single
        // field is at fault, so the error is reported under the address item itself
        if (
            string.IsNullOrWhiteSpace(SubBuildingName)
            && string.IsNullOrWhiteSpace(BuildingName)
            && string.IsNullOrWhiteSpace(BuildingNumber)
            && string.IsNullOrWhiteSpace(PoBox)
        )
            yield return new ValidationResult(
                $"At least one of {SubBuildingNameMember}, {BuildingNameMember}, {BuildingNumberMember} "
                    + $"and {PoBoxMember} is required."
            );
    }

    // ^[A-Z]{2}$, checked without a regex
    private static bool IsCountryCode(string value) => value.Length == 2 && value.All(char.IsAsciiLetterUpper);
}
