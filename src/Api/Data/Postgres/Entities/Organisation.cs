namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class Organisation
{
    public Guid OrganisationId { get; set; }
    public string OrganisationName { get; set; } = null!;
    public string? TradingName { get; set; }
    public string? CompaniesHouseNumber { get; set; }
    public string HomeNationCode { get; set; } = null!;
    public string? NationCode { get; set; }
    public string? MainActivitySic { get; set; }
    public string? OrganisationTypeCode { get; set; }
    public string? OrganisationSubTypeCode { get; set; }
    public string? OrganisationTypeName { get; set; }
    public int? OrganisationSize { get; set; }
    public long? Turnover { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public OrganisationType? OrganisationType { get; set; }
    public List<OrganisationAddress> Addresses { get; set; } = new();
}
