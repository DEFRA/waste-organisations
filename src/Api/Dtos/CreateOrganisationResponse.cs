using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// The body of a <c>201</c> from <c>POST /organisations</c>: the stored organisation, in the same
/// envelope as the request.
/// </summary>
public record CreateOrganisationResponse
{
    /// <summary>The stored organisation.</summary>
    [JsonPropertyName("organisation")]
    public required OrganisationResponse Organisation { get; init; }
}
