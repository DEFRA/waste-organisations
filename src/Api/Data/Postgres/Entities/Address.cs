using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Defra.WasteOrganisations.Api.Data.Postgres.Entities;

/// <summary>
/// An address in the "multiple text inputs" format of the GOV.UK Design System pattern "Addresses",
/// with its country (spec 0007, Decisions).
/// </summary>
public class Address : ITimestamped
{
    public Guid Id { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? TownOrCity { get; set; }
    public string? County { get; set; }
    public string? Postcode { get; set; }

    /// <summary>ISO 3166-1 alpha-2, for example <c>GB</c>. A UK address is one whose code is <c>GB</c>.</summary>
    public string? CountryIso { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("address");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.AddressLine1).HasColumnName("address_line_1");
        builder.Property(a => a.AddressLine2).HasColumnName("address_line_2");
        builder.Property(a => a.TownOrCity).HasColumnName("town_or_city");
        builder.Property(a => a.County).HasColumnName("county");
        builder.Property(a => a.Postcode).HasColumnName("postcode");
        builder.Property(a => a.CountryIso).HasColumnName("country_iso");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        // An address is stored once and shared: the six parts are unique together, a null part equal
        // to a null part (spec 0007, Decisions). Liquibase creates the index in changelog 1.4;
        // declaring it keeps the model true
        builder
            .HasIndex(a => new
            {
                a.AddressLine1,
                a.AddressLine2,
                a.TownOrCity,
                a.County,
                a.Postcode,
                a.CountryIso,
            })
            .HasDatabaseName("ux_address_values")
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
