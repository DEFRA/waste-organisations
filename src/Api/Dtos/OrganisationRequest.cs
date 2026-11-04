using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteOrganisations.Api.Dtos.Attributes;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// An organisation to create. Every string is trimmed before it is validated and stored.
/// </summary>
public record OrganisationRequest
{
    /// <summary>
    /// The registered name. Required for a type registered at Companies House; optional otherwise.
    /// Must not be blank when present.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The name the organisation trades under. Required for a type not registered at Companies
    /// House; optional otherwise. Must not be blank when present.
    /// </summary>
    [JsonPropertyName("tradingName")]
    public string? TradingName { get; init; }

    /// <summary>
    /// The Companies House number: two letters or digits followed by six digits, for example
    /// <c>12345678</c> or <c>SC123456</c>; upper-cased before it is checked. Required for a type
    /// registered at Companies House and must be absent for every other type.
    /// </summary>
    [JsonPropertyName("companiesHouseNumber")]
    public string? CompaniesHouseNumber { get; init; }

    /// <summary>
    /// The nation whose regulator the organisation deals with, which for an overseas organisation
    /// is not where it is based: <c>GB-ENG</c>, <c>GB-SCT</c>, <c>GB-WLS</c> or <c>GB-NIR</c>.
    /// </summary>
    [JsonPropertyName("regulatorNation")]
    [Required]
    [JsonEnumValue<RegulatorNation>]
    public string? RegulatorNation { get; init; }

    /// <summary>
    /// The organisation type, one of the published codes in upper snake case, matched exactly. The
    /// type decides whether <c>name</c> or <c>tradingName</c> is required, whether
    /// <c>companiesHouseNumber</c> is required or must be absent, and whether a <c>REGISTERED</c> or
    /// a <c>PRINCIPAL</c> address is required.
    /// </summary>
    [JsonPropertyName("organisationType")]
    [Required]
    [OrganisationTypeCode]
    public string? OrganisationType { get; init; }

    /// <summary>
    /// One to five addresses, each listing in <c>addressTypes</c> every kind it serves as; a kind
    /// may appear in one item only. An address is stored once and shared by every organisation
    /// that uses it, so two items holding the same address come back as one item carrying the
    /// kinds of both.
    /// </summary>
    [JsonPropertyName("addresses")]
    [Required]
    [MinLength(1, ErrorMessage = "The {0} field must hold at least one item.")]
    [MaxLength(5, ErrorMessage = "The {0} field must hold at most five items.")]
    public List<AddressRequest>? Addresses { get; init; }
}
