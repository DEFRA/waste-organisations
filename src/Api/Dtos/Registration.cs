using System.Text.Json.Serialization;

namespace Api.Dtos;

public record Registration
{
    [JsonPropertyName("status")]
    public required RegistrationStatus Status { get; init; }

    [JsonPropertyName("type")]
    public required RegistrationType Type { get; init; }

    [JsonPropertyName("registrationYear")]
    public int RegistrationYear { get; init; }
}
