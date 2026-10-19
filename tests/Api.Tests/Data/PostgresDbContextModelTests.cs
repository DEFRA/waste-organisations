using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Defra.WasteOrganisations.Api.Tests.Data;

// Builds the EF model on the Npgsql provider without a database: neither building the model nor
// adding to the change tracker opens a connection. Api.IntegrationTests prove the mapping against
// the Liquibase schema; these pin it where Sonar measures coverage
public class PostgresDbContextModelTests
{
    public static TheoryData<Type, string, string[]> Mappings =>
        new()
        {
            {
                typeof(Organisation),
                "organisation",
                [
                    "organisation_id",
                    "organisation_name",
                    "trading_name",
                    "companies_house_number",
                    "home_nation_code",
                    "nation_code",
                    "main_activity_sic",
                    "organisation_type_code",
                    "organisation_sub_type_code",
                    "organisation_type",
                    "organisation_size",
                    "turnover",
                    "valid_from",
                    "valid_to",
                    "created_at",
                    "updated_at",
                ]
            },
            {
                typeof(OrganisationType),
                "organisation_type",
                ["organisation_type_id", "organisation_id", "is_a_partnership", "organisation_type"]
            },
            { typeof(AddressUsageType), "address_usagetype", ["usage_type_id", "usage_type"] },
            {
                typeof(Address),
                "address",
                [
                    "address_id",
                    "address_line1",
                    "address_line2",
                    "city",
                    "county",
                    "postcode",
                    "country",
                    "state_province_or_region",
                    "phone_number",
                    "valid_from",
                    "valid_to",
                    "created_at",
                    "updated_at",
                ]
            },
            {
                typeof(OrganisationAddress),
                "organisation_address",
                [
                    "organisation_address_id",
                    "organisation_id",
                    "address_id",
                    "usage_type_id",
                    "valid_from",
                    "valid_to",
                    "created_at",
                    "updated_at",
                ]
            },
        };

    [Theory]
    [MemberData(nameof(Mappings))]
    public void Entity_ShouldMapToTableAndColumns(Type clrType, string table, string[] columns)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(clrType)!;
        var storeObject = StoreObjectIdentifier.Table(table);

        entityType.GetTableName().Should().Be(table);
        entityType.GetProperties().Select(p => p.GetColumnName(storeObject)).Should().BeEquivalentTo(columns);
    }

    [Fact]
    public void Keys_ShouldBeSetByTheApplication()
    {
        using var context = CreateContext();

        context
            .Model.GetEntityTypes()
            .SelectMany(e => e.FindPrimaryKey()!.Properties)
            .Should()
            .AllSatisfy(p => p.ValueGenerated.Should().Be(ValueGenerated.Never));
    }

    [Fact]
    public void ForeignKeys_ShouldRestrictDelete()
    {
        using var context = CreateContext();

        var foreignKeys = context.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).ToList();

        foreignKeys.Should().HaveCount(4);
        foreignKeys.Should().AllSatisfy(fk => fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict));
    }

    [Fact]
    public void Organisation_WhenAdded_ShouldFixUpForeignKeysFromNavigations()
    {
        using var context = CreateContext();
        var usageType = new AddressUsageType { UsageTypeId = Guid.CreateVersion7(), UsageType = "Registered" };
        var organisation = new Organisation
        {
            OrganisationId = Guid.CreateVersion7(),
            OrganisationName = "Organisation",
            HomeNationCode = "ENG",
            OrganisationType = new OrganisationType { OrganisationTypeId = Guid.CreateVersion7() },
        };
        var address = new Address { AddressId = Guid.CreateVersion7() };
        organisation.Addresses.Add(
            new OrganisationAddress
            {
                OrganisationAddressId = Guid.CreateVersion7(),
                Address = address,
                UsageType = usageType,
            }
        );

        context.Organisations.Add(organisation);

        organisation.OrganisationType.OrganisationId.Should().Be(organisation.OrganisationId);
        var organisationAddress = organisation.Addresses.Single();
        organisationAddress.OrganisationId.Should().Be(organisation.OrganisationId);
        organisationAddress.AddressId.Should().Be(address.AddressId);
        organisationAddress.UsageTypeId.Should().Be(usageType.UsageTypeId);
        organisationAddress.Organisation.Should().BeSameAs(organisation);
    }

    private static PostgresDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql("Host=127.0.0.1;Database=waste_organisations")
                .Options
        );
}
