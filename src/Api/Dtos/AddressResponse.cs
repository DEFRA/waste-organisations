using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// A stored address and the kinds of address it is for the organisation. Absent parts are omitted.
/// </summary>
public record AddressResponse : AddressResponseBase
{
    // Order -1 writes it before the members of the base record, where it was before they moved there
    /// <summary>Every kind of address this is for the organisation, in the order they were sent.</summary>
    [JsonPropertyName("addressTypes")]
    [JsonPropertyOrder(-1)]
    public required AddressType[] AddressTypes { get; init; }
}
