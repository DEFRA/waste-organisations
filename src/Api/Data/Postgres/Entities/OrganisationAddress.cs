namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class OrganisationAddress
{
    public Guid OrganisationAddressId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid AddressId { get; set; }
    public Guid UsageTypeId { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organisation Organisation { get; set; } = null!;
    public Address Address { get; set; } = null!;
    public AddressUsageType UsageType { get; set; } = null!;
}
