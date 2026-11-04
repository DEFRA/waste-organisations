using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// The nation whose regulator an organisation deals with, as an ISO 3166-2:GB code. For an
/// overseas organisation this is not where it is based.
/// </summary>
/// <remarks>
/// v1's <see cref="BusinessCountry"/> holds the same four codes but means where a business is, so
/// it is not reused. The v2 request binds <c>regulatorNation</c> as a string checked against these
/// wire values (<see cref="Attributes.JsonEnumValueAttribute{T}"/>), so an unknown value is a
/// validation error rather than an unreadable body; the response uses this enum.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RegulatorNation
{
    [JsonStringEnumMemberName("GB-ENG")]
    England,

    [JsonStringEnumMemberName("GB-SCT")]
    Scotland,

    [JsonStringEnumMemberName("GB-WLS")]
    Wales,

    [JsonStringEnumMemberName("GB-NIR")]
    NorthernIreland,
}
