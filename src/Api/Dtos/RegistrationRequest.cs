using System.Text.Json.Serialization;

namespace Api.Dtos;

public record RegistrationRequest
{
    [JsonPropertyName("status")]
    public required RegistrationStatus Status { get; init; }
}
