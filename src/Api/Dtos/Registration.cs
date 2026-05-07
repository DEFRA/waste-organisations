using System.ComponentModel;
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

public record RegistrationResponse : Registration
{
    [Description("ISO 8601 extended format with offset")]
    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; init; }

    [Description("ISO 8601 extended format with offset")]
    [JsonPropertyName("updated")]
    public DateTimeOffset Updated { get; init; }
}
