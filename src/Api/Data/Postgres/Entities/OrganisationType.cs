namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class OrganisationType
{
    public Guid OrganisationTypeId { get; set; }
    public Guid OrganisationId { get; set; }
    public bool IsAPartnership { get; set; }
    public string? OrganisationTypeName { get; set; }

    public Organisation Organisation { get; set; } = null!;
}
