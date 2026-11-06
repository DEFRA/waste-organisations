using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Services;

// The ux_address_values race against the Compose Postgres, made certain rather than left to timing:
// the address is stored first, then a create whose first look-up misses it, as a request that lost
// the race would. Api.Tests cover the retry's branches with a faked save; this proves the real
// unique violation is the one the service retries, and that the context can save again after it.
// Address rows are shared and never cleaned up, so the street holds a suffix unique to the test
public class PostgresOrganisationServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateAsync_WhenTheLookUpMissesAStoredAddress_ShouldRetryAndLinkTheStoredRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var provider = BuildServiceProvider();

        var first = await CreateAsync(provider, suffix, missFirstLookUp: false);
        var second = await CreateAsync(provider, suffix, missFirstLookUp: true);

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
        (
            await context.Addresses.CountAsync(
                address => address.Street != null && address.Street.Contains(suffix),
                cancellationToken
            )
        )
            .Should()
            .Be(1);

        // Unset before the retry, so the three are the retried save's instant (spec 0005, Persistence)
        retried.CreatedAt.Should().Be(retried.ValidFrom);
        retried.UpdatedAt.Should().Be(retried.ValidFrom);
        retried.Addresses.Single().ValidFrom.Should().Be(retried.ValidFrom);
    }

    // Each create in its own scope, so its own context, as each request has
    private static async Task<(Organisation Organisation, MissOnceService Service)> CreateAsync(
        ServiceProvider provider,
        string suffix,
        bool missFirstLookUp
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = provider.CreateAsyncScope();
        var service = new MissOnceService(
            scope.ServiceProvider.GetRequiredService<PostgresDbContext>(),
            missFirstLookUp
        );

        var request = new OrganisationRequest
        {
            Name = "Acme Waste Ltd",
            CompaniesHouseNumber = "12345678",
            RegulatorNation = "GB-ENG",
            OrganisationType = "LTD",
            Addresses =
            [
                new AddressRequest
                {
                    AddressTypes = [AddressType.Registered],
                    BuildingNumber = "12",
                    Street = $"High Street {suffix}",
                    Locality = "Testville",
                    PostalCode = "AB1 2CD",
                    CountryIso = "GB",
                },
            ],
        };
        var organisation = request.ToEntity(
            await service.OrganisationTypeIdAsync("LTD", cancellationToken),
            await service.UsageTypeIdsAsync(cancellationToken)
        );

        return (await service.CreateAsync(organisation, cancellationToken), service);
    }

    // The real service, whose first address look-up can be made to miss
    private sealed class MissOnceService(PostgresDbContext context, bool missFirstLookUp)
        : PostgresOrganisationService(context, NullLogger<PostgresOrganisationService>.Instance)
    {
        public int AddressLookups { get; private set; }

        protected override Task<Address?> FindAddressAsync(Address candidate, CancellationToken cancellationToken) =>
            ++AddressLookups == 1 && missFirstLookUp
                ? Task.FromResult<Address?>(null)
                : base.FindAddressAsync(candidate, cancellationToken);
    }
}
