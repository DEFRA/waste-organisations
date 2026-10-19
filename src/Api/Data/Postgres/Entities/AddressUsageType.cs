using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class AddressUsageType
{
    public Guid UsageTypeId { get; set; }
    public string UsageType { get; set; } = null!;
}

public class AddressUsageTypeConfiguration : IEntityTypeConfiguration<AddressUsageType>
{
    public void Configure(EntityTypeBuilder<AddressUsageType> builder)
    {
        builder.ToTable("address_usagetype");
        builder.HasKey(u => u.UsageTypeId);
        builder.Property(u => u.UsageTypeId).HasColumnName("usage_type_id").ValueGeneratedNever();
        builder.Property(u => u.UsageType).HasColumnName("usage_type");
    }
}
