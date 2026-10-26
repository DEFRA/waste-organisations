using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

/// <summary>
/// One legal form, from the seeded reference list (see <see cref="OrganisationTypeCodes"/>),
/// shared by every organisation of that form.
/// </summary>
public class OrganisationType
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
}

public class OrganisationTypeConfiguration : IEntityTypeConfiguration<OrganisationType>
{
    public void Configure(EntityTypeBuilder<OrganisationType> builder)
    {
        builder.ToTable("organisation_type");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.Code).HasColumnName("code");
    }
}
