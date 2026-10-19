using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

public class OrganisationSchemaTests : IntegrationTestBase
{
    // An organisation with its type and a Registered plus a Principal address saved through
    // PostgresDbContext must read back with every column and navigation equal, timestamps as UTC
    [Fact]
    public async Task Organisation_WithTypeAndTwoAddresses_RoundTrips()
    {
        await using var provider = BuildServiceProvider();
        var registeredId = await UsageTypeIdAsync(provider, "Registered");
        var principalId = await UsageTypeIdAsync(provider, "Principal");

        var organisation = NewOrganisation();
        organisation.OrganisationType = new OrganisationType
        {
            OrganisationTypeId = Guid.CreateVersion7(),
            OrganisationId = organisation.OrganisationId,
            IsAPartnership = true,
            OrganisationTypeName = "Partnership",
        };
        organisation.Addresses.Add(NewOrganisationAddress(organisation.OrganisationId, registeredId, "1 High Street"));
        organisation.Addresses.Add(NewOrganisationAddress(organisation.OrganisationId, principalId, "2 Mill Lane"));

        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var read = await context
            .Organisations.Include(o => o.OrganisationType)
            .Include(o => o.Addresses)
                .ThenInclude(a => a.Address)
            .Include(o => o.Addresses)
                .ThenInclude(a => a.UsageType)
            .SingleAsync(o => o.OrganisationId == organisation.OrganisationId, TestContext.Current.CancellationToken);

        read.OrganisationName.Should().Be(organisation.OrganisationName);
        read.TradingName.Should().Be(organisation.TradingName);
        read.CompaniesHouseNumber.Should().Be(organisation.CompaniesHouseNumber);
        read.HomeNationCode.Should().Be(organisation.HomeNationCode);
        read.NationCode.Should().Be(organisation.NationCode);
        read.MainActivitySic.Should().Be(organisation.MainActivitySic);
        read.OrganisationTypeCode.Should().Be(organisation.OrganisationTypeCode);
        read.OrganisationSubTypeCode.Should().Be(organisation.OrganisationSubTypeCode);
        read.OrganisationTypeName.Should().Be(organisation.OrganisationTypeName);
        read.OrganisationSize.Should().Be(organisation.OrganisationSize);
        read.Turnover.Should().Be(organisation.Turnover);
        read.ValidFrom.Should().Be(organisation.ValidFrom);
        read.ValidTo.Should().BeNull();
        read.CreatedAt.Should().Be(organisation.CreatedAt);
        read.UpdatedAt.Should().Be(organisation.UpdatedAt);
        read.ValidFrom.Kind.Should().Be(DateTimeKind.Utc);
        read.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        read.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);

        read.OrganisationType.Should().NotBeNull();
        read.OrganisationType.IsAPartnership.Should().BeTrue();
        read.OrganisationType.OrganisationTypeName.Should().Be("Partnership");

        read.Addresses.Should().HaveCount(2);
        read.Addresses.Select(a => a.UsageType.UsageType).Should().BeEquivalentTo("Registered", "Principal");
        foreach (var expected in organisation.Addresses)
        {
            var actual = read.Addresses.Single(a => a.OrganisationAddressId == expected.OrganisationAddressId);
            actual.UsageTypeId.Should().Be(expected.UsageTypeId);
            actual.Address.AddressLine1.Should().Be(expected.Address.AddressLine1);
            actual.Address.Postcode.Should().Be(expected.Address.Postcode);
            actual.ValidFrom.Should().Be(expected.ValidFrom);
            actual.ValidFrom.Kind.Should().Be(DateTimeKind.Utc);
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
        organisation.Addresses.Add(NewOrganisationAddress(organisation.OrganisationId, registeredId, "1 High Street"));
        await SaveOrganisationAsync(provider, organisation);

        await using (var failScope = provider.CreateAsyncScope())
        {
            var context = failScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            context.OrganisationAddresses.Add(
                NewOrganisationAddress(organisation.OrganisationId, registeredId, "2 Mill Lane")
            );
            var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await save.Should().ThrowAsync<DbUpdateException>();
        }

        await using (var closeScope = provider.CreateAsyncScope())
        {
            var context = closeScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            var current = await context.OrganisationAddresses.SingleAsync(
                a =>
                    a.OrganisationId == organisation.OrganisationId
                    && a.UsageTypeId == registeredId
                    && a.ValidTo == null,
                TestContext.Current.CancellationToken
            );
            current.ValidTo = UtcNowMicroseconds();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var acceptScope = provider.CreateAsyncScope())
        {
            var context = acceptScope.ServiceProvider.GetRequiredService<PostgresDbContext>();
            context.OrganisationAddresses.Add(
                NewOrganisationAddress(organisation.OrganisationId, registeredId, "2 Mill Lane")
            );
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
            NewOrganisationAddress(organisation.OrganisationId, Guid.CreateVersion7(), "9 Nowhere Road")
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
        organisation.Addresses.Add(NewOrganisationAddress(organisation.OrganisationId, registeredId, "1 High Street"));
        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var toDelete = await context.Organisations.SingleAsync(
            o => o.OrganisationId == organisation.OrganisationId,
            TestContext.Current.CancellationToken
        );
        context.Organisations.Remove(toDelete);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    // The check constraint rejects a row whose valid_to is earlier than its valid_from
    [Fact]
    public async Task AddressValidToBeforeValidFrom_IsRejectedByCheckConstraint()
    {
        await using var provider = BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var address = NewAddress("1 High Street");
        address.ValidTo = address.ValidFrom.AddDays(-1);
        context.Addresses.Add(address);

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    // The unique constraint on organisation_type.organisation_id allows one type per organisation
    [Fact]
    public async Task SecondOrganisationTypeForSameOrganisation_IsRejected()
    {
        await using var provider = BuildServiceProvider();

        var organisation = NewOrganisation();
        organisation.OrganisationType = new OrganisationType
        {
            OrganisationTypeId = Guid.CreateVersion7(),
            OrganisationId = organisation.OrganisationId,
            IsAPartnership = false,
            OrganisationTypeName = "Limited",
        };
        await SaveOrganisationAsync(provider, organisation);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        context.OrganisationTypes.Add(
            new OrganisationType
            {
                OrganisationTypeId = Guid.CreateVersion7(),
                OrganisationId = organisation.OrganisationId,
                IsAPartnership = true,
                OrganisationTypeName = "Partnership",
            }
        );

        var save = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
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
            .AddressUsageTypes.Where(u => u.UsageType == usageType)
            .Select(u => u.UsageTypeId)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static Organisation NewOrganisation()
    {
        var now = UtcNowMicroseconds();
        return new Organisation
        {
            OrganisationId = Guid.CreateVersion7(),
            OrganisationName = "Acme Waste Ltd",
            TradingName = "Acme",
            CompaniesHouseNumber = "12345678",
            HomeNationCode = "ENG",
            NationCode = "GB-ENG",
            MainActivitySic = "38110",
            OrganisationTypeCode = "LTD",
            OrganisationSubTypeCode = "PLC",
            OrganisationTypeName = "Limited",
            OrganisationSize = 3,
            Turnover = 1_000_000L,
            ValidFrom = now,
            CreatedAt = now,
            UpdatedAt = now,
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
            OrganisationAddressId = Guid.CreateVersion7(),
            OrganisationId = organisationId,
            UsageTypeId = usageTypeId,
            Address = NewAddress(addressLine1),
            ValidFrom = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static Address NewAddress(string addressLine1)
    {
        var now = UtcNowMicroseconds();
        return new Address
        {
            AddressId = Guid.CreateVersion7(),
            AddressLine1 = addressLine1,
            AddressLine2 = "Floor 2",
            City = "Testville",
            County = "Testshire",
            Postcode = "AB1 2CD",
            Country = "United Kingdom",
            StateProvinceOrRegion = "England",
            PhoneNumber = "01234 567890",
            ValidFrom = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // Postgres timestamptz keeps microseconds; truncating DateTime to the same resolution lets a
    // written value equal the value read back
    private static DateTime UtcNowMicroseconds()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - (now.Ticks % 10), DateTimeKind.Utc);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        // The Postgres section of src/Api/appsettings.Development.json, which compose.yml matches
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Postgres:DefaultHost"] = "127.0.0.1",
                    ["Postgres:Port"] = "5432",
                    ["Postgres:DatabaseName"] = "waste_organisations",
                    ["Postgres:User"] = "postgres",
                    ["Postgres:Password"] = "postgres",
                    ["Postgres:UseIamAuthentication"] = "false",
                }
            )
            .Build();

        return new ServiceCollection().AddPostgres(configuration).BuildServiceProvider();
    }
}
