using System.Diagnostics.CodeAnalysis;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Microsoft.EntityFrameworkCore;

namespace Defra.WasteOrganisations.Api.Data;

/// <summary>
/// Maps the organisation schema owned by the Liquibase changelog in <c>changelog/</c>:
/// <c>organisation</c>, <c>organisation_type</c>, <c>address_usagetype</c>, <c>address</c> and
/// <c>organisation_address</c>. EF Core never creates or migrates the schema.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<OrganisationType> OrganisationTypes => Set<OrganisationType>();
    public DbSet<AddressUsageType> AddressUsageTypes => Set<AddressUsageType>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<OrganisationAddress> OrganisationAddresses => Set<OrganisationAddress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrganisationConfiguration());
        modelBuilder.ApplyConfiguration(new OrganisationTypeConfiguration());
        modelBuilder.ApplyConfiguration(new AddressUsageTypeConfiguration());
        modelBuilder.ApplyConfiguration(new AddressConfiguration());
        modelBuilder.ApplyConfiguration(new OrganisationAddressConfiguration());
    }
}
