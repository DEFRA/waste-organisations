using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class AddressUsageType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

public class AddressUsageTypeConfiguration : IEntityTypeConfiguration<AddressUsageType>
{
    public void Configure(EntityTypeBuilder<AddressUsageType> builder)
    {
        builder.ToTable("address_usagetype");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.Name).HasColumnName("name");
    }
}
