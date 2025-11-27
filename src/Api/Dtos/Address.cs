using System.Text.Json.Serialization;

namespace Api.Dtos;

public record Address
{
    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; init; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; init; }

    [JsonPropertyName("town")]
    public string? Town { get; init; }

    [JsonPropertyName("county")]
    public string? County { get; init; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }
}
