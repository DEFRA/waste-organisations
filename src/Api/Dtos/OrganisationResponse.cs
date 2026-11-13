using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

// Null members are omitted rather than written as null, so consumers must treat an absent member
// as null

/// <summary>
/// A stored organisation, with every string as it was stored: trimmed, and
/// <c>companiesHouseNumber</c> upper-cased.
/// </summary>
public record OrganisationResponse : OrganisationResponseBase
{
    /// <summary>The organisation type code.</summary>
    [JsonPropertyName("organisationType")]
    public required string OrganisationType { get; init; }

    /// <summary>When this version of the organisation became valid. ISO 8601 extended format with offset.</summary>
    [JsonPropertyName("validFrom")]
    public required DateTimeOffset ValidFrom { get; init; }

    /// <summary>
    /// When this version of the organisation stopped being valid; absent while it is current.
    /// ISO 8601 extended format with offset.
    /// </summary>
    [JsonPropertyName("validTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ValidTo { get; init; }

    /// <summary>When the organisation was stored. ISO 8601 extended format with offset.</summary>
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the organisation was last changed. ISO 8601 extended format with offset.</summary>
    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// One item per stored address, in the order each first appeared in the request. Two request
    /// items holding the same address come back as one item carrying the kinds of both.
    /// </summary>
    [JsonPropertyName("addresses")]
    public required AddressResponse[] Addresses { get; init; }
}
