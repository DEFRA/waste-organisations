using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// A stored organisation with its current addresses. Null members are omitted rather than written
/// as null, so consumers must treat an absent member as null.
/// </summary>
public record OrganisationDetailsResponse : OrganisationResponseBase
{
    /// <summary>The organisation type code; absent when the organisation has no type.</summary>
    [JsonPropertyName("organisationType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrganisationType { get; init; }

    /// <summary>
    /// When this version of the organisation stopped being valid; absent while it is current.
    /// ISO 8601 extended format with offset.
    /// </summary>
    [JsonPropertyName("validTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ValidTo { get; init; }

    /// <summary>
    /// One item per current address, carrying every kind it currently is for the organisation; an
    /// empty array when there is none. The order is not guaranteed.
    /// </summary>
    [JsonPropertyName("addresses")]
    public required AddressDetailsResponse[] Addresses { get; init; }
}
