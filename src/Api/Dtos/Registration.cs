using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

public record Registration
{
    [JsonPropertyName("status")]
    public required RegistrationStatus Status { get; init; }

    [JsonPropertyName("type")]
    public required RegistrationType Type { get; init; }

    [JsonPropertyName("registrationYear")]
    [Range(Api.RegistrationYear.Minimum, Api.RegistrationYear.Maximum)]
    public required int RegistrationYear { get; init; }
}
