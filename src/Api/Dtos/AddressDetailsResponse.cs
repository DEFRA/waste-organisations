using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// A current address of an organisation and the kinds of address it is for the organisation now.
/// Absent parts are omitted.
/// </summary>
public record AddressDetailsResponse : AddressResponseBase
{
    // Order -1 writes it before the members of the base record
    /// <summary>
    /// Every kind of address this currently is for the organisation. The order is not guaranteed.
    /// </summary>
    [JsonPropertyName("addressTypes")]
    [JsonPropertyOrder(-1)]
    public required AddressType[] AddressTypes { get; init; }
}
