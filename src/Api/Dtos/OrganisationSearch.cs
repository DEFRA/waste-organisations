using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

public record OrganisationSearch
{
    [JsonPropertyName("organisations")]
    public Organisation[] Organisations { get; init; } = [];
}
