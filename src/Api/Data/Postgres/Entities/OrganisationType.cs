using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class OrganisationType
{
    public Guid OrganisationTypeId { get; set; }
    public Guid OrganisationId { get; set; }
    public bool IsAPartnership { get; set; }
    public string? OrganisationTypeName { get; set; }

    public Organisation Organisation { get; set; } = null!;
}

public class OrganisationTypeConfiguration : IEntityTypeConfiguration<OrganisationType>
{
    public void Configure(EntityTypeBuilder<OrganisationType> builder)
    {
        builder.ToTable("organisation_type");
        builder.HasKey(t => t.OrganisationTypeId);
        builder.Property(t => t.OrganisationTypeId).HasColumnName("organisation_type_id").ValueGeneratedNever();
        builder.Property(t => t.OrganisationId).HasColumnName("organisation_id");
        builder.Property(t => t.IsAPartnership).HasColumnName("is_a_partnership");
        builder.Property(t => t.OrganisationTypeName).HasColumnName("organisation_type");

        builder
            .HasOne(t => t.Organisation)
            .WithOne(o => o.OrganisationType)
            .HasForeignKey<OrganisationType>(t => t.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
