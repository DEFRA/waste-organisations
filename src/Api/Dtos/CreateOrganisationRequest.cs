using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

// Required members are nullable with [Required] rather than C# `required`, which v1 uses: a missing
// `required` member makes the body unreadable (a `detail` 400), whereas the v2 contract reports it
// as a validation error under its JSON path

/// <summary>
/// The body of <c>POST /organisations</c>: the organisation to create, wrapped in an envelope.
/// </summary>
public record CreateOrganisationRequest
{
    internal const string OrganisationMember = "organisation";

    /// <summary>The organisation to create.</summary>
    [JsonPropertyName(OrganisationMember)]
    [Required]
    public OrganisationRequest? Organisation { get; init; }
}
