namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class Address
{
    public Guid AddressId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public string? Postcode { get; set; }
    public string? Country { get; set; }
    public string? StateProvinceOrRegion { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
