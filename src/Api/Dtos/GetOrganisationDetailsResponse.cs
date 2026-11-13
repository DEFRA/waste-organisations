using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>The body of a <c>200</c> from the v2 <c>GET /organisations/{id}</c>.</summary>
public record GetOrganisationDetailsResponse
{
    /// <summary>The organisation with the requested id: always exactly one item.</summary>
    [JsonPropertyName("organisations")]
    public required OrganisationDetailsResponse[] Organisations { get; init; }
}
