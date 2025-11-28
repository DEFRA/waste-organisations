using System.Text.Json.Serialization;

namespace Api.Dtos;

public record OrganisationSearch
{
    [JsonPropertyName("organisations")]
    public Organisation[] Organisations { get; init; } = [];
}
