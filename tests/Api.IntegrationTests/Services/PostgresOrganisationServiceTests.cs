using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Services;

// The service against the Compose Postgres, with failures made certain rather than left to timing.
// The ux_address_values race: the address is stored first, then a create whose look-ups miss it, as
// a request that lost the race would. Api.Tests cover the retry's branches with a faked save; this
// proves the real unique violation is the one the service retries, that the context can save again
// after it, and that a failed save stores nothing. Address rows are shared and never cleaned up, so
// each street holds a suffix unique to the test
public class PostgresOrganisationServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateAsync_WhenTheLookUpMissesAStoredAddress_ShouldRetryAndLinkTheStoredRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var suffix = NewSuffix();
        await using var provider = BuildServiceProvider();

        var first = await CreateAsync(provider, suffix, missedLookUps: 0);
        var second = await CreateAsync(provider, suffix, missedLookUps: 1);

        second.Service.AddressLookups.Should().Be(2);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var addressId = await context
            .OrganisationAddresses.Where(link => link.OrganisationId == first.Organisation.Id)
            .Select(link => link.AddressId)
            .SingleAsync(cancellationToken);
        var retried = await context
            .Organisations.AsNoTracking()
            .Include(organisation => organisation.Addresses)
            .SingleAsync(organisation => organisation.Id == second.Organisation.Id, cancellationToken);

        retried.Addresses.Should().ContainSingle().Which.AddressId.Should().Be(addressId);
        (await CountAddressesAsync(context, suffix)).Should().Be(1);

        // Unset before the retry, so the three are the retried save's instant (spec 0005, Persistence)
        retried.CreatedAt.Should().Be(retried.ValidFrom);
        retried.UpdatedAt.Should().Be(retried.ValidFrom);
        retried.Addresses.Single().ValidFrom.Should().Be(retried.ValidFrom);
    }

    // Two current links of one kind break ux_organisation_address_current, which the organisation_address
    // insert can only reach once the organisation and address rows it references are inserted. The
    // violation is not retried, and the save's transaction takes the earlier inserts with it
    [Fact]
    public async Task CreateAsync_WhenALinkFailsAfterTheOrganisationIsInserted_ShouldStoreNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var suffix = NewSuffix();
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = new MissingLookUpsService(scope.ServiceProvider.GetRequiredService<PostgresDbContext>(), 0);

        var organisation = await BuildAsync(
            service,
            Address(suffix, "12", AddressType.Registered),
            Address(suffix, "14", AddressType.Principal)
        );
        organisation.Addresses[1].UsageTypeId = organisation.Addresses[0].UsageTypeId;

        var create = () => service.CreateAsync(organisation, cancellationToken);

        var exception = await create.Should().ThrowAsync<DbUpdateException>();
        exception
            .Which.InnerException.Should()
            .BeOfType<PostgresException>()
            .Which.ConstraintName.Should()
            .Be("ux_organisation_address_current");
        await AssertNothingStoredAsync(provider, organisation.Id, suffix, expectedAddresses: 0);
    }

    // Both attempts miss the stored address, so both fail on ux_address_values and the second is
    // thrown; neither leaves a row behind
    [Fact]
    public async Task CreateAsync_WhenTheRetryAlsoFails_ShouldStoreNothing()
    {
        var suffix = NewSuffix();
        await using var provider = BuildServiceProvider();

        await CreateAsync(provider, suffix, missedLookUps: 0);

        await using var scope = provider.CreateAsyncScope();
        var service = new MissingLookUpsService(scope.ServiceProvider.GetRequiredService<PostgresDbContext>(), 2);
        var organisation = await BuildAsync(service, Address(suffix, "12", AddressType.Registered));

        var create = () => service.CreateAsync(organisation, TestContext.Current.CancellationToken);

        var exception = await create.Should().ThrowAsync<DbUpdateException>();
        exception
            .Which.InnerException.Should()
            .BeOfType<PostgresException>()
            .Which.ConstraintName.Should()
            .Be("ux_address_values");
        service.AddressLookups.Should().Be(2);
        await AssertNothingStoredAsync(provider, organisation.Id, suffix, expectedAddresses: 1);
    }

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..12];

    // Each create in its own scope, so its own context, as each request has
    private static async Task<(Organisation Organisation, MissingLookUpsService Service)> CreateAsync(
        ServiceProvider provider,
        string suffix,
        int missedLookUps
    )
    {
        await using var scope = provider.CreateAsyncScope();
        var service = new MissingLookUpsService(
            scope.ServiceProvider.GetRequiredService<PostgresDbContext>(),
            missedLookUps
        );
        var organisation = await BuildAsync(service, Address(suffix, "12", AddressType.Registered));

        return (await service.CreateAsync(organisation, TestContext.Current.CancellationToken), service);
    }

    private static async Task<Organisation> BuildAsync(
        PostgresOrganisationService service,
        params AddressRequest[] addresses
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = new OrganisationRequest
        {
            Name = "Acme Waste Ltd",
            CompaniesHouseNumber = "12345678",
            RegulatorNation = "GB-ENG",
            OrganisationType = "LTD",
            Addresses = [.. addresses],
        };

        return request.ToEntity(
            await service.OrganisationTypeIdAsync("LTD", cancellationToken),
            await service.UsageTypeIdsAsync(cancellationToken)
        );
    }

    private static AddressRequest Address(string suffix, string buildingNumber, AddressType kind) =>
        new()
        {
            AddressTypes = [kind],
            BuildingNumber = buildingNumber,
            Street = $"High Street {suffix}",
            Locality = "Testville",
            PostalCode = "AB1 2CD",
            CountryIso = "GB",
        };

    private static Task<int> CountAddressesAsync(PostgresDbContext context, string suffix) =>
        context.Addresses.CountAsync(
            address => address.Street != null && address.Street.Contains(suffix),
            TestContext.Current.CancellationToken
        );

    // A fresh context, so nothing the failed one still tracks is read back
    private static async Task AssertNothingStoredAsync(
        ServiceProvider provider,
        Guid organisationId,
        string suffix,
        int expectedAddresses
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        (await context.Organisations.AnyAsync(o => o.Id == organisationId, cancellationToken)).Should().BeFalse();
        (await context.OrganisationAddresses.AnyAsync(link => link.OrganisationId == organisationId, cancellationToken))
            .Should()
            .BeFalse();
        (await CountAddressesAsync(context, suffix)).Should().Be(expectedAddresses);
    }

    // The real service, whose first address look-ups can be made to miss
    private sealed class MissingLookUpsService(PostgresDbContext context, int missedLookUps)
        : PostgresOrganisationService(context, NullLogger<PostgresOrganisationService>.Instance)
    {
        public int AddressLookups { get; private set; }

        protected override Task<Address?> FindAddressAsync(Address candidate, CancellationToken cancellationToken) =>
            ++AddressLookups <= missedLookUps
                ? Task.FromResult<Address?>(null)
                : base.FindAddressAsync(candidate, cancellationToken);
    }
}
