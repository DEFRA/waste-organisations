using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RegistrationType
{
    [JsonStringEnumMemberName("SMALL_PRODUCER")]
    SmallProducer,

    [JsonStringEnumMemberName("LARGE_PRODUCER")]
    LargeProducer,

    [JsonStringEnumMemberName("COMPLIANCE_SCHEME")]
    ComplianceScheme,

    [JsonStringEnumMemberName("REPROCESSOR")]
    Reprocessor,

    [JsonStringEnumMemberName("EXPORTER")]
    Exporter,
}
