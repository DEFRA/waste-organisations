using System.Text.Json.Serialization;

namespace Api.Dtos;

public record Registration
{
    [JsonPropertyName("status")]
    public required RegistrationStatus Status { get; init; }

    [JsonPropertyName("type")]
    public required RegistrationType Type { get; init; }

    [JsonPropertyName("submissionYear")]
    public int SubmissionYear { get; init; }
}
