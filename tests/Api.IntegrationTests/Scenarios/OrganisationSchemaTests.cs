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
        read.HomeNationCode.Should().Be(organisation.HomeNationCode);
        read.NationCode.Should().Be(organisation.NationCode);
        read.MainActivitySic.Should().Be(organisation.MainActivitySic);
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
            actual.Address.AddressLine1.Should().Be(expected.Address.AddressLine1);
            actual.Address.AddressLine2.Should().Be(expected.Address.AddressLine2);
            actual.Address.City.Should().Be(expected.Address.City);
            actual.Address.County.Should().Be(expected.Address.County);
            actual.Address.Postcode.Should().Be(expected.Address.Postcode);
            actual.Address.Country.Should().Be(expected.Address.Country);
            actual.Address.StateProvinceOrRegion.Should().Be(expected.Address.StateProvinceOrRegion);
            actual.Address.PhoneNumber.Should().Be(expected.Address.PhoneNumber);
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
            HomeNationCode = "ENG",
            CreatedAt = supplied,
            UpdatedAt = supplied,
        };
        organisation.Addresses.Add(
            new OrganisationAddress
            {
                Id = Guid.CreateVersion7(),
                UsageTypeId = registeredId,
                Address = new Address { Id = Guid.CreateVersion7(), AddressLine1 = "1 High Street" },
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
            HomeNationCode = "ENG",
            NationCode = "GB-ENG",
            MainActivitySic = "38110",
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

    private static Address NewAddress(string addressLine1) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AddressLine1 = addressLine1,
            AddressLine2 = "Floor 2",
            City = "Testville",
            County = "Testshire",
            Postcode = "AB1 2CD",
            Country = "United Kingdom",
            StateProvinceOrRegion = "England",
            PhoneNumber = "01234 567890",
        };

    // Postgres timestamptz keeps microseconds; truncating DateTime to the same resolution lets a
    // written value equal the value read back
    private static DateTime UtcNowMicroseconds()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - (now.Ticks % 10), DateTimeKind.Utc);
    }
}
