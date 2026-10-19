namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class AddressUsageType
{
    public Guid UsageTypeId { get; set; }
    public string UsageType { get; set; } = null!;
}
