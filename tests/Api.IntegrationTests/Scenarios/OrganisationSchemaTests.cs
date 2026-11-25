using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

public class OrganisationSchemaTests : IntegrationTestBase
{
    // An organisation with a seeded type and a Registered plus a Principal address saved through
    // PostgresDbContext must read back with every column and navigation equal, timestamps as UTC
    [Fact]
    public async Task Organisation_WithTypeAndTwoAddresses_RoundTrips()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "REGISTERED");
        var principalId = await UsageTypeIdAsync(provider, "PRINCIPAL");

        var organisation = NewOrganisation(CompaniesHouseNumbers.Organisation_WithTypeAndTwoAddresses_RoundTrips);
        organisation.OrganisationTypeId = await OrganisationTypeIdAsync(provider, "LLP");
        organisation.Addresses.Add(NewOrganisationAddress(organisation.Id, registeredId, "1 High Street"));
        organisation.Addresses.Add(NewOrganisationAddress(organisation.Id, principalId, "2 Mill Lane"));

        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var read = await context
            .Organisations.Include(o => o.OrganisationType)
            .Include(o => o.Addresses)
                .ThenInclude(a => a.Address)
            .Include(o => o.Addresses)
                .ThenInclude(a => a.UsageType)
            .SingleAsync(o => o.Id == organisation.Id, TestContext.Current.CancellationToken);

        read.Name.Should().Be(organisation.Name);
        read.TradingName.Should().Be(organisation.TradingName);
        read.CompaniesHouseNumber.Should().Be(organisation.CompaniesHouseNumber);
        read.RegulatorNation.Should().Be(organisation.RegulatorNation);
        read.OrganisationTypeId.Should().Be(organisation.OrganisationTypeId);
        read.ValidFrom.Should().Be(organisation.ValidFrom);
        read.ValidTo.Should().BeNull();
        read.CreatedAt.Should().Be(organisation.CreatedAt);
        read.UpdatedAt.Should().Be(organisation.UpdatedAt);
        read.ValidFrom.Kind.Should().Be(DateTimeKind.Utc);
        read.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        read.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);

        read.OrganisationType.Should().NotBeNull();
        read.OrganisationType.Code.Should().Be("LLP");

        read.Addresses.Should().HaveCount(2);
        read.Addresses.Select(a => a.UsageType.Name).Should().BeEquivalentTo("REGISTERED", "PRINCIPAL");
        foreach (var expected in organisation.Addresses)
        {
            var actual = read.Addresses.Single(a => a.Id == expected.Id);
            actual.UsageTypeId.Should().Be(expected.UsageTypeId);
            actual.Address.AddressLine1.Should().Be(expected.Address.AddressLine1);
            actual.Address.AddressLine2.Should().Be(expected.Address.AddressLine2);
            actual.Address.TownOrCity.Should().Be(expected.Address.TownOrCity);
            actual.Address.County.Should().Be(expected.Address.County);
            actual.Address.Postcode.Should().Be(expected.Address.Postcode);
            actual.Address.CountryIso.Should().Be(expected.Address.CountryIso);
            actual.Address.CreatedAt.Should().Be(expected.Address.CreatedAt);
            actual.Address.UpdatedAt.Should().Be(expected.Address.UpdatedAt);
            actual.ValidFrom.Should().Be(expected.ValidFrom);
            actual.ValidFrom.Kind.Should().Be(DateTimeKind.Utc);
            actual.CreatedAt.Should().Be(expected.CreatedAt);
            actual.UpdatedAt.Should().Be(expected.UpdatedAt);
        }
    }

    // ux_organisation_address_current allows one current (valid_to IS NULL) address per organisation
    // and usage type: a second current Registered row is rejected until the first is closed
    [Fact]
    public async Task SecondCurrentRegisteredAddress_IsRejectedThenAcceptedWhenFirstClosed()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "REGISTERED");

        var organisation = NewOrganisation(
            CompaniesHouseNumbers.SecondCurrentRegisteredAddress_IsRejectedThenAcceptedWhenFirstClosed
        );
        organisation.Addresses.Add(NewOrganisationAddress(organisation.Id, registeredId, "1 High Street"));
        await SaveOrganisationAsync(provider, organisation);

        await using (var failScope = provider.CreateAsyncScope())
        {
            var context = failScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            context.OrganisationAddresses.Add(NewOrganisationAddress(organisation.Id, registeredId, "2 Mill Lane"));
            var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await ShouldViolateAsync(save, "ux_organisation_address_current");
        }

        await using (var closeScope = provider.CreateAsyncScope())
        {
            var context = closeScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            var current = await context.OrganisationAddresses.SingleAsync(
                a => a.OrganisationId == organisation.Id && a.UsageTypeId == registeredId && a.ValidTo == null,
                TestContext.Current.CancellationToken
            );
            current.ValidTo = UtcNowMicroseconds();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var acceptScope = provider.CreateAsyncScope())
        {
            var context = acceptScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            context.OrganisationAddresses.Add(NewOrganisationAddress(organisation.Id, registeredId, "2 Mill Lane"));
            var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await save.Should().NotThrowAsync();
        }
    }

    // The usage_type_id foreign key rejects an organisation_address pointing at no usage type row
    [Fact]
    public async Task OrganisationAddress_WithUnknownUsageType_IsRejectedByForeignKey()
    {
        await using var provider = BuildServiceProvider();
        var organisation = NewOrganisation(
            CompaniesHouseNumbers.OrganisationAddress_WithUnknownUsageType_IsRejectedByForeignKey
        );
        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.OrganisationAddresses.Add(
            NewOrganisationAddress(organisation.Id, Guid.CreateVersion7(), "9 Nowhere Road")
        );

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "fk_organisation_address_usage_type");
    }

    // The organisation_id foreign key rejects an organisation_address for no organisation row
    [Fact]
    public async Task OrganisationAddress_WithUnknownOrganisation_IsRejectedByForeignKey()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "REGISTERED");

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.OrganisationAddresses.Add(
            NewOrganisationAddress(Guid.CreateVersion7(), registeredId, "9 Nowhere Road")
        );

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "fk_organisation_address_organisation");
    }

    // ON DELETE RESTRICT keeps an organisation that still has addresses from being deleted
    [Fact]
    public async Task DeletingOrganisationWithAddresses_IsRejectedByRestrict()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "REGISTERED");

        var organisation = NewOrganisation(
            CompaniesHouseNumbers.DeletingOrganisationWithAddresses_IsRejectedByRestrict
        );
        organisation.Addresses.Add(NewOrganisationAddress(organisation.Id, registeredId, "1 High Street"));
        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var toDelete = await context.Organisations.SingleAsync(
            o => o.Id == organisation.Id,
            TestContext.Current.CancellationToken
        );
        context.Organisations.Remove(toDelete);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "fk_organisation_address_organisation");
    }

    // The check constraint rejects a row whose valid_to is earlier than its valid_from
    [Fact]
    public async Task OrganisationAddressValidToBeforeValidFrom_IsRejectedByCheckConstraint()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "REGISTERED");
        var organisation = NewOrganisation(
            CompaniesHouseNumbers.OrganisationAddressValidToBeforeValidFrom_IsRejectedByCheckConstraint
        );
        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var organisationAddress = NewOrganisationAddress(organisation.Id, registeredId, "1 High Street");
        organisationAddress.ValidTo = organisationAddress.ValidFrom.AddDays(-1);
        context.OrganisationAddresses.Add(organisationAddress);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "ck_organisation_address_valid_to");
    }

    // The organisation_type_id foreign key rejects an organisation whose type is not a seeded row
    [Fact]
    public async Task Organisation_WithUnknownOrganisationType_IsRejectedByForeignKey()
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var organisation = NewOrganisation(
            CompaniesHouseNumbers.Organisation_WithUnknownOrganisationType_IsRejectedByForeignKey
        );
        organisation.OrganisationTypeId = Guid.CreateVersion7();
        context.Organisations.Add(organisation);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "fk_organisation_organisation_type");
    }

    // The columns have no database default, and only the service sets created_at and updated_at:
    // TimestampInterceptor writes the clock's time on insert whatever the entity holds, fills
    // valid_from when unset, and on update moves updated_at but never created_at
    [Fact]
    public async Task Organisation_AuditTimestamps_AreSetOnlyByTheService()
    {
        var supplied = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created = new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(created);
        await using var provider = BuildServiceProvider(timeProvider);
        var registeredId = await UsageTypeIdAsync(provider, "REGISTERED");

        var organisation = new Organisation
        {
            Id = Guid.CreateVersion7(),
            Name = "Untimed Ltd",
            RegulatorNation = "GB-ENG",
            CreatedAt = supplied,
            UpdatedAt = supplied,
        };
        organisation.Addresses.Add(
            new OrganisationAddress
            {
                Id = Guid.CreateVersion7(),
                UsageTypeId = registeredId,
                Address = new Address
                {
                    Id = Guid.CreateVersion7(),
                    AddressLine1 = Unique("1 High Street"),
                    TownOrCity = "Testville",
                    Postcode = "AB1 2CD",
                    CountryIso = "GB",
                },
            }
        );
        await SaveOrganisationAsync(provider, organisation);

        var updated = created.AddHours(1);
        timeProvider.SetUtcNow(updated);
        await using (var updateScope = provider.CreateAsyncScope())
        {
            var context = updateScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            var toUpdate = await context.Organisations.SingleAsync(
                o => o.Id == organisation.Id,
                TestContext.Current.CancellationToken
            );
            toUpdate.TradingName = "Untimed";
            toUpdate.CreatedAt = supplied;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var scope = provider.CreateAsyncScope();
        var readContext = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var read = await readContext
            .Organisations.Include(o => o.Addresses)
                .ThenInclude(a => a.Address)
            .SingleAsync(o => o.Id == organisation.Id, TestContext.Current.CancellationToken);

        read.ValidFrom.Should().Be(created.UtcDateTime);
        read.CreatedAt.Should().Be(created.UtcDateTime);
        read.UpdatedAt.Should().Be(updated.UtcDateTime);
        var organisationAddress = read.Addresses.Single();
        organisationAddress.ValidFrom.Should().Be(created.UtcDateTime);
        organisationAddress.CreatedAt.Should().Be(created.UtcDateTime);
        organisationAddress.UpdatedAt.Should().Be(created.UtcDateTime);
        organisationAddress.Address.CreatedAt.Should().Be(created.UtcDateTime);
        organisationAddress.Address.UpdatedAt.Should().Be(created.UtcDateTime);
    }

    // regulator_nation is one of the four UK nations in ISO 3166-2:GB, enforced by
    // ck_organisation_regulator_nation (ck_organisation_home_nation until 1.2 renamed the column)
    [Theory]
    [InlineData("ENG")]
    [InlineData("GB-XXX")]
    public async Task Organisation_WithRegulatorNationOutsideTheUkNations_IsRejectedByCheckConstraint(
        string regulatorNation
    )
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var organisation = NewOrganisation(
            CompaniesHouseNumbers.Organisation_WithRegulatorNationOutsideTheUkNations_IsRejectedByCheckConstraint
        );
        organisation.RegulatorNation = regulatorNation;
        context.Organisations.Add(organisation);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "ck_organisation_regulator_nation");
    }

    // 1.2 made name nullable, but ck_organisation_name still needs one unless the organisation has no
    // Companies House number and a non-blank trading name: a sole trader may be known only by the name
    // it trades under
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Organisation_WithoutNameButWithTradingNameAndNoNumber_IsAccepted(string? name)
    {
        await using var provider = BuildServiceProvider();
        var organisation = NewOrganisation(null);
        organisation.Name = name;

        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var saved = await context
            .Organisations.AsNoTracking()
            .SingleAsync(o => o.Id == organisation.Id, TestContext.Current.CancellationToken);
        saved.Name.Should().Be(name);
        saved.TradingName.Should().Be("Acme");
    }

    [Theory]
    [InlineData(
        null,
        "Acme",
        CompaniesHouseNumbers.Organisation_WithoutName_IsRejectedByCheckConstraintUnlessTradingNameAndNoNumber_NullName
    )]
    [InlineData(
        "  ",
        "Acme",
        CompaniesHouseNumbers.Organisation_WithoutName_IsRejectedByCheckConstraintUnlessTradingNameAndNoNumber_BlankName
    )]
    [InlineData(null, null, null)]
    [InlineData(null, "", null)]
    [InlineData(null, "  ", null)]
    [InlineData("  ", null, null)]
    public async Task Organisation_WithoutName_IsRejectedByCheckConstraintUnlessTradingNameAndNoNumber(
        string? name,
        string? tradingName,
        string? companiesHouseNumber
    )
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var organisation = NewOrganisation(null);
        organisation.Name = name;
        organisation.TradingName = tradingName;
        organisation.CompaniesHouseNumber = companiesHouseNumber;
        context.Organisations.Add(organisation);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "ck_organisation_name");
    }

    // country_iso is ISO 3166-1 alpha-2: ck_address_country_iso allows only two upper-case letters
    [Theory]
    [InlineData("GBR")]
    [InlineData("gb")]
    public async Task Address_WithCountryIsoNotAlpha2_IsRejectedByCheckConstraint(string countryIso)
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.Addresses.Add(NewAddress("High Street", countryIso));

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "ck_address_country_iso");
    }

    // addressLine1 is required: ck_address_address_line_1 rejects a null, empty or blank value
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Address_WithoutAddressLine1_IsRejectedByCheckConstraint(string? addressLine1)
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var address = NewAddress("High Street");
        address.AddressLine1 = addressLine1;
        context.Addresses.Add(address);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "ck_address_address_line_1");
    }

    // townOrCity is required: ck_address_town_or_city rejects a null, empty or blank value
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Address_WithoutTownOrCity_IsRejectedByCheckConstraint(string? townOrCity)
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var address = NewAddress("High Street");
        address.TownOrCity = townOrCity;
        context.Addresses.Add(address);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "ck_address_town_or_city");
    }

    // A GB address needs a postcode: ck_address_uk_postcode rejects a null, empty or blank value
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Address_GbWithoutPostcode_IsRejectedByCheckConstraint(string? postcode)
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var address = NewAddress("High Street");
        address.Postcode = postcode;
        context.Addresses.Add(address);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "ck_address_uk_postcode");
    }

    // Changelog 1.4 drops the eight old address parts: the address table holds the six GDS columns and none of the eight old ones
    [Fact]
    public async Task Address_Table_HasTheGdsColumnsAndNoOldParts()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var columns = await context
            .Database.SqlQueryRaw<string>(
                "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'address'"
            )
            .ToListAsync(TestContext.Current.CancellationToken);

        columns
            .Should()
            .NotContain([
                "sub_building_name",
                "building_name",
                "building_number",
                "street",
                "po_box",
                "locality",
                "region",
                "postal_code",
            ]);
        columns
            .Should()
            .Contain(["address_line_1", "address_line_2", "town_or_city", "county", "postcode", "country_iso"]);
    }

    // Addresses without the optional parts are accepted: an overseas address needs no postcode,
    // and addressLine2 and county are optional. The changelog 1.4 checks are tested apart
    [Theory]
    [InlineData("overseas without postcode")]
    [InlineData("no addressLine2 or county")]
    public async Task Address_WithoutOptionalParts_IsAccepted(string kind)
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.Addresses.Add(AddressOfKind(kind));

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().NotThrowAsync();
    }

    // A unique townOrCity keeps the accepted cases clear of ux_address_values on a second run
    private static Address AddressOfKind(string kind) =>
        kind switch
        {
            "overseas without postcode" => new Address
            {
                Id = Guid.CreateVersion7(),
                AddressLine1 = "12 Harbour Road",
                TownOrCity = Unique("Hong Kong"),
                CountryIso = "HK",
            },
            "no addressLine2 or county" => new Address
            {
                Id = Guid.CreateVersion7(),
                AddressLine1 = "1 High Street",
                TownOrCity = Unique("Testville"),
                Postcode = "AB1 2CD",
                CountryIso = "GB",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    // An address is stored once: ux_address_values, over six parts since changelog 1.4, rejects
    // a second row with the same six parts, and with NULLS NOT DISTINCT an absent part equals an
    // absent part, which a plain unique index would let through
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Address_WithTheSameSixParts_IsRejectedByUniqueIndex(bool withAbsentParts)
    {
        await using var provider = BuildServiceProvider();
        var address = NewAddress("High Street");
        if (withAbsentParts)
        {
            address.AddressLine2 = null;
            address.County = null;
        }

        await using (var saveScope = provider.CreateAsyncScope())
        {
            var context = saveScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            context.Addresses.Add(address);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var scope = provider.CreateAsyncScope();
        var duplicateContext = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        duplicateContext.Addresses.Add(CopyOf(address));

        var save = async () => await duplicateContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await ShouldViolateAsync(save, "ux_address_values");
    }

    // Any one of the six parts of changelog 1.4 that differs, in value, in presence or only in
    // case, makes another address: the comparison is exact, so "High Street" and "HIGH STREET" are two rows
    [Theory]
    [InlineData("addressLine1")]
    [InlineData("addressLine1 in upper case")]
    [InlineData("addressLine2 absent")]
    [InlineData("townOrCity")]
    [InlineData("county")]
    [InlineData("postcode")]
    [InlineData("countryIso")]
    public async Task Address_DifferingInOnePart_IsAccepted(string part)
    {
        await using var provider = BuildServiceProvider();
        var address = NewAddress("High Street");
        var other = CopyOf(address);
        switch (part)
        {
            case "addressLine1":
                other.AddressLine1 = Unique("Unit 5, Mill House");
                break;
            case "addressLine1 in upper case":
                other.AddressLine1 = address.AddressLine1?.ToUpperInvariant();
                break;
            case "addressLine2 absent":
                other.AddressLine2 = null;
                break;
            case "townOrCity":
                other.TownOrCity = "Testham";
                break;
            case "county":
                other.County = "Otherfordshire";
                break;
            case "postcode":
                other.Postcode = "AB1 2CE";
                break;
            case "countryIso":
                other.CountryIso = "FR";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(part), part, null);
        }

        await using (var saveScope = provider.CreateAsyncScope())
        {
            var context = saveScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            context.Addresses.Add(address);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var scope = provider.CreateAsyncScope();
        var otherContext = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        otherContext.Addresses.Add(other);

        var save = async () => await otherContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().NotThrowAsync();
    }

    // An address row belongs to no organisation: two organisations link the same row through their
    // own organisation_address rows, and the row is stored once. Pinned so that a later story which
    // changes an address re-links the organisation instead of updating a row others share
    [Fact]
    public async Task TwoOrganisations_MayLinkTheSameAddressRow()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "REGISTERED");

        var first = NewOrganisation(CompaniesHouseNumbers.TwoOrganisations_MayLinkTheSameAddressRow_First);
        var firstLink = NewOrganisationAddress(first.Id, registeredId, "1 High Street");
        first.Addresses.Add(firstLink);
        await SaveOrganisationAsync(provider, first);

        var second = NewOrganisation(CompaniesHouseNumbers.TwoOrganisations_MayLinkTheSameAddressRow_Second);
        second.Addresses.Add(
            new OrganisationAddress
            {
                Id = Guid.CreateVersion7(),
                OrganisationId = second.Id,
                UsageTypeId = registeredId,
                AddressId = firstLink.Address.Id,
                ValidFrom = UtcNowMicroseconds(),
            }
        );
        await SaveOrganisationAsync(provider, second);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var linkedOrganisations = await context
            .OrganisationAddresses.Where(a => a.AddressId == firstLink.Address.Id)
            .Select(a => a.OrganisationId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var rows = await context.Addresses.CountAsync(
            a => a.AddressLine1 == firstLink.Address.AddressLine1,
            TestContext.Current.CancellationToken
        );

        linkedOrganisations.Should().BeEquivalentTo([first.Id, second.Id]);
        rows.Should().Be(1);
    }

    private static async Task SaveOrganisationAsync(ServiceProvider provider, Organisation organisation)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.Organisations.Add(organisation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> UsageTypeIdAsync(ServiceProvider provider, string usageType)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        return await context
            .AddressUsageTypes.Where(u => u.Name == usageType)
            .Select(u => u.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> OrganisationTypeIdAsync(ServiceProvider provider, string code)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        return await context
            .OrganisationTypes.Where(t => t.Code == code)
            .Select(t => t.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static Organisation NewOrganisation(string? companiesHouseNumber)
    {
        var now = UtcNowMicroseconds();
        return new Organisation
        {
            Id = Guid.CreateVersion7(),
            Name = "Acme Waste Ltd",
            TradingName = "Acme",
            CompaniesHouseNumber = companiesHouseNumber,
            RegulatorNation = "GB-ENG",
            ValidFrom = now,
        };
    }

    private static OrganisationAddress NewOrganisationAddress(
        Guid organisationId,
        Guid usageTypeId,
        string addressLine1
    )
    {
        var now = UtcNowMicroseconds();
        return new OrganisationAddress
        {
            Id = Guid.CreateVersion7(),
            OrganisationId = organisationId,
            UsageTypeId = usageTypeId,
            Address = NewAddress(addressLine1),
            ValidFrom = now,
        };
    }

    // ux_address_values allows one row per set of the six parts of changelog 1.4, so each address a
    // test inserts carries a unique addressLine1, and a test that means to share a row reuses the same one
    private static Address NewAddress(string addressLine1, string? countryIso = "GB") =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AddressLine1 = Unique(addressLine1),
            AddressLine2 = "Mill House",
            TownOrCity = "Testville",
            County = "Testshire",
            Postcode = "AB1 2CD",
            CountryIso = countryIso,
        };

    private static Address CopyOf(Address address) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            TownOrCity = address.TownOrCity,
            County = address.County,
            Postcode = address.Postcode,
            CountryIso = address.CountryIso,
        };

    private static string Unique(string value) => $"{value} {Guid.NewGuid():N}";

    // Naming the constraint keeps a test from passing because another constraint, such as
    // ux_address_values, rejected the row first
    private static async Task ShouldViolateAsync(Func<Task> save, string constraintName)
    {
        var thrown = await save.Should().ThrowAsync<DbUpdateException>();
        thrown
            .Which.InnerException.Should()
            .BeOfType<PostgresException>()
            .Which.ConstraintName.Should()
            .Be(constraintName);
    }

    // Postgres timestamptz keeps microseconds; truncating DateTime to the same resolution lets a
    // written value equal the value read back
    private static DateTime UtcNowMicroseconds()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - (now.Ticks % 10), DateTimeKind.Utc);
    }
}
