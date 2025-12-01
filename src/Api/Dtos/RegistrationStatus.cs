using System.Text.Json.Serialization;

namespace Api.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RegistrationStatus
{
    [JsonStringEnumMemberName("REGISTERED")]
    Registered,

    [JsonStringEnumMemberName("CANCELLED")]
    Cancelled,
}
