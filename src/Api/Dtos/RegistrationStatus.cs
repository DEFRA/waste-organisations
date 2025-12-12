using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RegistrationStatus
{
    [JsonStringEnumMemberName("REGISTERED")]
    Registered,

    [JsonStringEnumMemberName("CANCELLED")]
    Cancelled,
}
