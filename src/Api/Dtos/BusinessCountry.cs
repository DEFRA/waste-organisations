using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BusinessCountry
{
    [JsonStringEnumMemberName("GB-ENG")]
    England,

    [JsonStringEnumMemberName("GB-NIR")]
    NorthernIreland,

    [JsonStringEnumMemberName("GB-SCT")]
    Scotland,

    [JsonStringEnumMemberName("GB-WLS")]
    Wales,
}
