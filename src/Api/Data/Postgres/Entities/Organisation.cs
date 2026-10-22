using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class Organisation : IValidFrom, ITimestamped
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? TradingName { get; set; }
    public string? CompaniesHouseNumber { get; set; }

    /// <summary>ISO 3166-2:GB: <c>GB-ENG</c>, <c>GB-SCT</c>, <c>GB-WLS</c> or <c>GB-NIR</c>.</summary>
    public string HomeNation { get; set; } = null!;
    public Guid? OrganisationTypeId { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public OrganisationType? OrganisationType { get; set; }
    public List<OrganisationAddress> Addresses { get; set; } = new();
}

public class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("organisation");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.Name).HasColumnName("name");
        builder.Property(o => o.TradingName).HasColumnName("trading_name");
        builder.Property(o => o.CompaniesHouseNumber).HasColumnName("companies_house_number");
        builder.Property(o => o.HomeNation).HasColumnName("home_nation");
        builder.Property(o => o.OrganisationTypeId).HasColumnName("organisation_type_id");
        builder.Property(o => o.ValidFrom).HasColumnName("valid_from");
        builder.Property(o => o.ValidTo).HasColumnName("valid_to");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder
            .HasOne(o => o.OrganisationType)
            .WithMany()
            .HasForeignKey(o => o.OrganisationTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
