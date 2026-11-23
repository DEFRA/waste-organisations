using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// An address of an organisation and the kinds of address it is for the organisation. Absent parts
/// are omitted.
/// </summary>
public record AddressResponse
{
    /// <summary>Every kind of address this is for the organisation.</summary>
    [JsonPropertyName("addressTypes")]
    public required AddressType[] AddressTypes { get; init; }

    /// <summary>The first line of the address, such as the building and the street.</summary>
    [JsonPropertyName("addressLine1")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AddressLine1 { get; init; }

    /// <summary>The second line of the address.</summary>
    [JsonPropertyName("addressLine2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AddressLine2 { get; init; }

    /// <summary>The town or city.</summary>
    [JsonPropertyName("townOrCity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TownOrCity { get; init; }

    /// <summary>The county; for an address outside the UK, the state or province.</summary>
    [JsonPropertyName("county")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? County { get; init; }

    /// <summary>
    /// The postcode. A <c>GB</c> postcode is the outward code, one space and the inward code, such
    /// as <c>AB1 2CD</c>.
    /// </summary>
    [JsonPropertyName("postcode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Postcode { get; init; }

    /// <summary>
    /// The country as an ISO 3166-1 alpha-2 code, upper-case. <c>GB</c> is the United Kingdom,
    /// Northern Ireland included.
    /// </summary>
    [JsonPropertyName("countryIso")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CountryIso { get; init; }
}
