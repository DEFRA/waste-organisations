using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

public class Organisation : IValidFrom, ITimestamped
{
    public Guid Id { get; set; }
    public string OrganisationName { get; set; } = null!;
    public string? TradingName { get; set; }
    public string? CompaniesHouseNumber { get; set; }
    public string HomeNationCode { get; set; } = null!;
    public string? NationCode { get; set; }
    public string? MainActivitySic { get; set; }
    public string? OrganisationTypeCode { get; set; }
    public string? OrganisationSubTypeCode { get; set; }
    public string? OrganisationTypeName { get; set; }
    public int? OrganisationSize { get; set; }
    public long? Turnover { get; set; }
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
        builder.Property(o => o.OrganisationName).HasColumnName("organisation_name");
        builder.Property(o => o.TradingName).HasColumnName("trading_name");
        builder.Property(o => o.CompaniesHouseNumber).HasColumnName("companies_house_number");
        builder.Property(o => o.HomeNationCode).HasColumnName("home_nation_code");
        builder.Property(o => o.NationCode).HasColumnName("nation_code");
        builder.Property(o => o.MainActivitySic).HasColumnName("main_activity_sic");
        builder.Property(o => o.OrganisationTypeCode).HasColumnName("organisation_type_code");
        builder.Property(o => o.OrganisationSubTypeCode).HasColumnName("organisation_sub_type_code");
        builder.Property(o => o.OrganisationTypeName).HasColumnName("organisation_type");
        builder.Property(o => o.OrganisationSize).HasColumnName("organisation_size");
        builder.Property(o => o.Turnover).HasColumnName("turnover");
        builder.Property(o => o.ValidFrom).HasColumnName("valid_from");
        builder.Property(o => o.ValidTo).HasColumnName("valid_to");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
    }
}
