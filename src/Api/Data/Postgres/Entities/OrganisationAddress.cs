using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class OrganisationAddress : IValidFrom, ITimestamped
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

public class OrganisationAddressConfiguration : IEntityTypeConfiguration<OrganisationAddress>
{
    public void Configure(EntityTypeBuilder<OrganisationAddress> builder)
    {
        builder.ToTable("organisation_address");
        builder.HasKey(a => a.OrganisationAddressId);
        builder.Property(a => a.OrganisationAddressId).HasColumnName("organisation_address_id").ValueGeneratedNever();
        builder.Property(a => a.OrganisationId).HasColumnName("organisation_id");
        builder.Property(a => a.AddressId).HasColumnName("address_id");
        builder.Property(a => a.UsageTypeId).HasColumnName("usage_type_id");
        builder.Property(a => a.ValidFrom).HasColumnName("valid_from");
        builder.Property(a => a.ValidTo).HasColumnName("valid_to");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder
            .HasOne(a => a.Organisation)
            .WithMany(o => o.Addresses)
            .HasForeignKey(a => a.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Address).WithMany().HasForeignKey(a => a.AddressId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.UsageType).WithMany().HasForeignKey(a => a.UsageTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
