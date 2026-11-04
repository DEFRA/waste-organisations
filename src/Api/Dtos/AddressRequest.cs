using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// An address in the Defra standard address model, with the country as a two-letter code, and
/// the kinds of address it is. At least one of <c>subBuildingName</c>, <c>buildingName</c>,
/// <c>buildingNumber</c> and <c>poBox</c> must be given, so the premises can be identified.
/// </summary>
public record AddressRequest
{
    /// <summary>
    /// Every kind of address this is, each at most once. One item may list all five kinds.
    /// </summary>
    [JsonPropertyName("addressTypes")]
    [Required]
    [MinLength(1, ErrorMessage = "The {0} field must hold at least one item.")]
    public List<AddressType>? AddressTypes { get; init; }

    /// <summary>A flat or unit within the building.</summary>
    [JsonPropertyName("subBuildingName")]
    public string? SubBuildingName { get; init; }

    /// <summary>The name of the building.</summary>
    [JsonPropertyName("buildingName")]
    public string? BuildingName { get; init; }

    /// <summary>The building number, which may hold letters or a range, such as <c>12A</c> or <c>3-5</c>.</summary>
    [JsonPropertyName("buildingNumber")]
    public string? BuildingNumber { get; init; }

    /// <summary>The street.</summary>
    [JsonPropertyName("street")]
    [Required]
    public string? Street { get; init; }

    /// <summary>The town or city.</summary>
    [JsonPropertyName("locality")]
    [Required]
    public string? Locality { get; init; }

    /// <summary>The county, state or province.</summary>
    [JsonPropertyName("region")]
    public string? Region { get; init; }

    /// <summary>
    /// The postcode. Required when <c>countryIso</c> is <c>GB</c>; optional otherwise, because some
    /// countries have no postcodes. Not checked against a format.
    /// </summary>
    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; init; }

    /// <summary>A PO box.</summary>
    [JsonPropertyName("poBox")]
    public string? PoBox { get; init; }

    /// <summary>
    /// The country as an ISO 3166-1 alpha-2 code, upper-cased before it is checked. <c>GB</c> is
    /// the United Kingdom, Northern Ireland included.
    /// </summary>
    [JsonPropertyName("countryIso")]
    [Required]
    public string? CountryIso { get; init; }
}
