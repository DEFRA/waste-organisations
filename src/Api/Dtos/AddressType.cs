using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>A kind of address an organisation has.</summary>
/// <remarks>
/// The wire value is also the <c>address_usagetype.name</c> it is stored under, so
/// <c>ToJsonValue()</c> gives the name to look up. Only the wire names are read: an integer or a
/// numeric string is an unreadable body (<see cref="AddressTypeJsonConverter"/>).
/// </remarks>
[JsonConverter(typeof(AddressTypeJsonConverter))]
public enum AddressType
{
    [JsonStringEnumMemberName("REGISTERED")]
    Registered,

    [JsonStringEnumMemberName("PRINCIPAL")]
    Principal,

    [JsonStringEnumMemberName("AUDIT")]
    Audit,

    [JsonStringEnumMemberName("SERVICE_OF_NOTICE")]
    ServiceOfNotice,

    [JsonStringEnumMemberName("CORRESPONDENCE")]
    Correspondence,
}
