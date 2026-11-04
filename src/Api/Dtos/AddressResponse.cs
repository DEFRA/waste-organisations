using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// A stored address and the kinds of address it is for the organisation. Absent parts are omitted.
/// </summary>
public record AddressResponse
{
    /// <summary>Every kind of address this is for the organisation, in the order they were sent.</summary>
    [JsonPropertyName("addressTypes")]
    public required AddressType[] AddressTypes { get; init; }

    /// <summary>
    /// The address on one line, computed in postal order with blank parts skipped and without the
    /// country; never stored.
    /// </summary>
    [JsonPropertyName("addressLine")]
    public required string AddressLine { get; init; }

    /// <summary>A flat or unit within the building.</summary>
    [JsonPropertyName("subBuildingName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubBuildingName { get; init; }

    /// <summary>The name of the building.</summary>
    [JsonPropertyName("buildingName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BuildingName { get; init; }

    /// <summary>The building number.</summary>
    [JsonPropertyName("buildingNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BuildingNumber { get; init; }

    /// <summary>The street.</summary>
    [JsonPropertyName("street")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Street { get; init; }

    /// <summary>The town or city.</summary>
    [JsonPropertyName("locality")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Locality { get; init; }

    /// <summary>The county, state or province.</summary>
    [JsonPropertyName("region")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Region { get; init; }

    /// <summary>The postcode.</summary>
    [JsonPropertyName("postalCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PostalCode { get; init; }

    /// <summary>A PO box.</summary>
    [JsonPropertyName("poBox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PoBox { get; init; }

    /// <summary>The country as an ISO 3166-1 alpha-2 code, upper-case.</summary>
    [JsonPropertyName("countryIso")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CountryIso { get; init; }

    /// <summary>Whether <c>countryIso</c> is <c>GB</c>, the United Kingdom, Northern Ireland included.</summary>
    [JsonPropertyName("isUkAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsUkAddress { get; init; }
}
