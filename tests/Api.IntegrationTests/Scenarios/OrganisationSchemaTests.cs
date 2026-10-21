using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

public class OrganisationSchemaTests : IntegrationTestBase
{
    // An organisation with a seeded type and a Registered plus a Principal address saved through
    // PostgresDbContext must read back with every column and navigation equal, timestamps as UTC
    [Fact]
    public async Task Organisation_WithTypeAndTwoAddresses_RoundTrips()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "Registered");
        var principalId = await UsageTypeIdAsync(provider, "Principal");

        var organisation = NewOrganisation();
        organisation.OrganisationTypeId = await OrganisationTypeIdAsync(provider, "llp");
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
        read.HomeNation.Should().Be(organisation.HomeNation);
        read.OrganisationTypeId.Should().Be(organisation.OrganisationTypeId);
        read.ValidFrom.Should().Be(organisation.ValidFrom);
        read.ValidTo.Should().BeNull();
        read.CreatedAt.Should().Be(organisation.CreatedAt);
        read.UpdatedAt.Should().Be(organisation.UpdatedAt);
        read.ValidFrom.Kind.Should().Be(DateTimeKind.Utc);
        read.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        read.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);

        read.OrganisationType.Should().NotBeNull();
        read.OrganisationType.Code.Should().Be("llp");

        read.Addresses.Should().HaveCount(2);
        read.Addresses.Select(a => a.UsageType.Name).Should().BeEquivalentTo("Registered", "Principal");
        foreach (var expected in organisation.Addresses)
        {
            var actual = read.Addresses.Single(a => a.Id == expected.Id);
            actual.UsageTypeId.Should().Be(expected.UsageTypeId);
            actual.Address.AddressLine.Should().Be(expected.Address.AddressLine);
            actual.Address.SubBuildingName.Should().Be(expected.Address.SubBuildingName);
            actual.Address.BuildingName.Should().Be(expected.Address.BuildingName);
            actual.Address.BuildingNumber.Should().Be(expected.Address.BuildingNumber);
            actual.Address.Street.Should().Be(expected.Address.Street);
            actual.Address.Locality.Should().Be(expected.Address.Locality);
            actual.Address.Region.Should().Be(expected.Address.Region);
            actual.Address.PostalCode.Should().Be(expected.Address.PostalCode);
            actual.Address.PoBox.Should().Be(expected.Address.PoBox);
            actual.Address.CountryIso.Should().Be(expected.Address.CountryIso);
            actual.Address.CountryName.Should().Be(expected.Address.CountryName);
            actual.Address.IsUkAddress.Should().BeTrue();
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
        var registeredId = await UsageTypeIdAsync(provider, "Registered");

        var organisation = NewOrganisation();
        organisation.Addresses.Add(NewOrganisationAddress(organisation.Id, registeredId, "1 High Street"));
        await SaveOrganisationAsync(provider, organisation);

        await using (var failScope = provider.CreateAsyncScope())
        {
            var context = failScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            context.OrganisationAddresses.Add(NewOrganisationAddress(organisation.Id, registeredId, "2 Mill Lane"));
            var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await save.Should().ThrowAsync<DbUpdateException>();
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
        var organisation = NewOrganisation();
        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.OrganisationAddresses.Add(
            NewOrganisationAddress(organisation.Id, Guid.CreateVersion7(), "9 Nowhere Road")
        );

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    // The organisation_id foreign key rejects an organisation_address for no organisation row
    [Fact]
    public async Task OrganisationAddress_WithUnknownOrganisation_IsRejectedByForeignKey()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "Registered");

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.OrganisationAddresses.Add(
            NewOrganisationAddress(Guid.CreateVersion7(), registeredId, "9 Nowhere Road")
        );

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    // ON DELETE RESTRICT keeps an organisation that still has addresses from being deleted
    [Fact]
    public async Task DeletingOrganisationWithAddresses_IsRejectedByRestrict()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "Registered");

        var organisation = NewOrganisation();
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
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    // The check constraint rejects a row whose valid_to is earlier than its valid_from
    [Fact]
    public async Task OrganisationAddressValidToBeforeValidFrom_IsRejectedByCheckConstraint()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "Registered");
        var organisation = NewOrganisation();
        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var organisationAddress = NewOrganisationAddress(organisation.Id, registeredId, "1 High Street");
        organisationAddress.ValidTo = organisationAddress.ValidFrom.AddDays(-1);
        context.OrganisationAddresses.Add(organisationAddress);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    // The organisation_type_id foreign key rejects an organisation whose type is not a seeded row
    [Fact]
    public async Task Organisation_WithUnknownOrganisationType_IsRejectedByForeignKey()
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var organisation = NewOrganisation();
        organisation.OrganisationTypeId = Guid.CreateVersion7();
        context.Organisations.Add(organisation);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
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
        var registeredId = await UsageTypeIdAsync(provider, "Registered");

        var organisation = new Organisation
        {
            Id = Guid.CreateVersion7(),
            Name = "Untimed Ltd",
            HomeNation = "GB-ENG",
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
                    BuildingNumber = "1",
                    Street = "High Street",
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

    // home_nation is one of the four UK nations in ISO 3166-2:GB, enforced by ck_organisation_home_nation
    [Theory]
    [InlineData("ENG")]
    [InlineData("GB-XXX")]
    public async Task Organisation_WithHomeNationOutsideTheUkNations_IsRejectedByCheckConstraint(string homeNation)
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var organisation = NewOrganisation();
        organisation.HomeNation = homeNation;
        context.Organisations.Add(organisation);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
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
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    // Real addresses without a building number, a street or a postcode are accepted: only a UK address
    // needs a postcode, and any one of number, building name, unit, PO box or address line identifies
    // the premises
    [Theory]
    [InlineData("named building only")]
    [InlineData("PO box")]
    [InlineData("overseas without postcode")]
    public async Task Address_WithoutBuildingNumberOrPostcode_IsAccepted(string kind)
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.Addresses.Add(AddressOfKind(kind));

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().NotThrowAsync();
    }

    // ck_address_uk_postal_code rejects a UK address with a missing or blank postcode, and
    // ck_address_premises rejects an address with nothing (or only blanks) to identify the premises
    [Theory]
    [InlineData("UK without postcode")]
    [InlineData("UK with blank postcode")]
    [InlineData("no premises")]
    [InlineData("blank premises")]
    public async Task Address_WithoutRequiredParts_IsRejectedByCheckConstraint(string kind)
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.Addresses.Add(AddressOfKind(kind));

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    private static Address AddressOfKind(string kind) =>
        kind switch
        {
            "named building only" => new Address
            {
                Id = Guid.CreateVersion7(),
                BuildingName = "Rose Farm",
                Locality = "Little Testing",
                PostalCode = "AB1 2CD",
                CountryIso = "GB",
            },
            "PO box" => new Address
            {
                Id = Guid.CreateVersion7(),
                PoBox = "PO Box 99",
                Locality = "Testville",
                PostalCode = "AB1 9ZZ",
                CountryIso = "GB",
            },
            "overseas without postcode" => new Address
            {
                Id = Guid.CreateVersion7(),
                BuildingNumber = "12",
                Street = "Harbour Road",
                Locality = "Hong Kong",
                CountryIso = "HK",
            },
            "UK without postcode" => new Address
            {
                Id = Guid.CreateVersion7(),
                BuildingNumber = "1",
                Street = "High Street",
                CountryIso = "GB",
            },
            "UK with blank postcode" => new Address
            {
                Id = Guid.CreateVersion7(),
                BuildingNumber = "1",
                Street = "High Street",
                PostalCode = "  ",
                CountryIso = "GB",
            },
            "no premises" => new Address
            {
                Id = Guid.CreateVersion7(),
                Street = "High Street",
                PostalCode = "AB1 2CD",
                CountryIso = "GB",
            },
            "blank premises" => new Address
            {
                Id = Guid.CreateVersion7(),
                BuildingNumber = " ",
                BuildingName = "",
                Street = "High Street",
                PostalCode = "AB1 2CD",
                CountryIso = "GB",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    // is_uk_address is generated from country_iso: true for GB, false for any other country and
    // null when the country is unknown; EF reads it back after the save
    [Theory]
    [InlineData("GB", true)]
    [InlineData("FR", false)]
    [InlineData(null, null)]
    public async Task Address_IsUkAddress_IsGeneratedFromCountryIso(string? countryIso, bool? expected)
    {
        await using var provider = BuildServiceProvider();
        var address = NewAddress("High Street", countryIso);

        await using (var saveScope = provider.CreateAsyncScope())
        {
            var context = saveScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            context.Addresses.Add(address);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        address.IsUkAddress.Should().Be(expected);
        await using var scope = provider.CreateAsyncScope();
        var read = await scope
            .ServiceProvider.GetRequiredService<PostgresDbContext>()
            .Addresses.SingleAsync(a => a.Id == address.Id, TestContext.Current.CancellationToken);
        read.IsUkAddress.Should().Be(expected);
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

    private static Organisation NewOrganisation()
    {
        var now = UtcNowMicroseconds();
        return new Organisation
        {
            Id = Guid.CreateVersion7(),
            Name = "Acme Waste Ltd",
            TradingName = "Acme",
            CompaniesHouseNumber = "12345678",
            HomeNation = "GB-ENG",
            ValidFrom = now,
        };
    }

    private static OrganisationAddress NewOrganisationAddress(Guid organisationId, Guid usageTypeId, string street)
    {
        var now = UtcNowMicroseconds();
        return new OrganisationAddress
        {
            Id = Guid.CreateVersion7(),
            OrganisationId = organisationId,
            UsageTypeId = usageTypeId,
            Address = NewAddress(street),
            ValidFrom = now,
        };
    }

    private static Address NewAddress(string street, string? countryIso = "GB") =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AddressLine = "Rear entrance",
            SubBuildingName = "Unit 4",
            BuildingName = "Mill House",
            BuildingNumber = "12A",
            Street = street,
            Locality = "Testville",
            Region = "Testshire",
            PostalCode = "AB1 2CD",
            PoBox = "PO Box 99",
            CountryIso = countryIso,
            CountryName = countryIso is null ? null : "Test country",
        };

    // Postgres timestamptz keeps microseconds; truncating DateTime to the same resolution lets a
    // written value equal the value read back
    private static DateTime UtcNowMicroseconds()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - (now.Ticks % 10), DateTimeKind.Utc);
    }
}
