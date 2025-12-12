using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

public record RegistrationRequest
{
    [JsonPropertyName("status")]
    public required RegistrationStatus Status { get; init; }
}
