using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("address");
        builder.HasKey(a => a.AddressId);
        builder.Property(a => a.AddressId).HasColumnName("address_id").ValueGeneratedNever();
        builder.Property(a => a.AddressLine1).HasColumnName("address_line1");
        builder.Property(a => a.AddressLine2).HasColumnName("address_line2");
        builder.Property(a => a.City).HasColumnName("city");
        builder.Property(a => a.County).HasColumnName("county");
        builder.Property(a => a.Postcode).HasColumnName("postcode");
        builder.Property(a => a.Country).HasColumnName("country");
        builder.Property(a => a.StateProvinceOrRegion).HasColumnName("state_province_or_region");
        builder.Property(a => a.PhoneNumber).HasColumnName("phone_number");
        builder.Property(a => a.ValidFrom).HasColumnName("valid_from");
        builder.Property(a => a.ValidTo).HasColumnName("valid_to");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
    }
}
