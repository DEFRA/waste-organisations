using System.Text.Json.Serialization;

namespace Api.Dtos;

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
