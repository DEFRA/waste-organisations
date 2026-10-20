using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class OrganisationType
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public bool IsAPartnership { get; set; }
    public string? Name { get; set; }

    public Organisation Organisation { get; set; } = null!;
}

public class OrganisationTypeConfiguration : IEntityTypeConfiguration<OrganisationType>
{
    public void Configure(EntityTypeBuilder<OrganisationType> builder)
    {
        builder.ToTable("organisation_type");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.OrganisationId).HasColumnName("organisation_id");
        builder.Property(t => t.IsAPartnership).HasColumnName("is_a_partnership");
        builder.Property(t => t.Name).HasColumnName("name");

        builder
            .HasOne(t => t.Organisation)
            .WithOne(o => o.OrganisationType)
            .HasForeignKey<OrganisationType>(t => t.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
