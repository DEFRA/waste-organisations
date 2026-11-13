using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.Tests.Services;

// The context is built on the Npgsql provider without a database, as in PostgresDbContextModelTests:
// attaching and adding open no connection, the save is faked and the look-ups are stubbed. What
// matters here is what is tracked at each save, which decides what EF Core would insert: a stored
// address must be Unchanged, never re-inserted, and only a ux_address_values race is retried.
// Api.IntegrationTests prove the queries against Postgres, and the race with a look-up made to
// miss a stored address (Services/PostgresOrganisationServiceTests)
public class PostgresOrganisationServiceTests
{
    private static readonly Guid OrganisationTypeId = Guid.NewGuid();

    private static readonly IReadOnlyList<(Guid Id, string Name)> UsageTypes = Enum.GetValues<AddressType>()
        .Select(kind => (Guid.NewGuid(), kind.ToJsonValue()))
        .ToList();

    private static readonly IReadOnlyDictionary<AddressType, Guid> UsageTypeIds = UsageTypes.ToDictionary(
        usageType => usageType.Name.FromJsonValue<AddressType>(),
        usageType => usageType.Id
    );

    private FakeContext Context { get; } = new();
    private TestService Subject { get; }

    public PostgresOrganisationServiceTests() => Subject = new TestService(Context);

    [Fact]
    public async Task CreateAsync_WithANewAddress_ShouldAddItAndSaveOnce()
    {
        var organisation = NewOrganisation(Item([AddressType.Registered, AddressType.Principal]));
        var candidate = organisation.Addresses[0].Address;

        var result = await Subject.CreateAsync(organisation, TestContext.Current.CancellationToken);

        Context.Saves.Should().ContainSingle().Which.Should().Equal((candidate, EntityState.Added));
        Subject.AddressLookups.Should().Be(1);
        result.Addresses.Should().AllSatisfy(link => link.Address.Should().BeSameAs(candidate));
    }

    [Fact]
    public async Task CreateAsync_WithTwoItemsHoldingTheSameAddress_ShouldLookItUpOnce()
    {
        var organisation = NewOrganisation(Item([AddressType.Registered]), Item([AddressType.Principal]));

        await Subject.CreateAsync(organisation, TestContext.Current.CancellationToken);

        Subject.AddressLookups.Should().Be(1);
        Context.Saves.Should().ContainSingle().Which.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_WithAStoredAddress_ShouldLinkTheStoredRowAndAddNoAddress()
    {
        var organisation = NewOrganisation(
            Item([AddressType.Registered, AddressType.Principal]),
            Item([AddressType.Correspondence]) with
            {
                Street = "Mill Lane",
            }
        );
        var stored = StoredAddress();
        Subject.StoredAddresses.Enqueue(stored);

        var result = await Subject.CreateAsync(organisation, TestContext.Current.CancellationToken);

        var save = Context.Saves.Should().ContainSingle().Subject;
        save.Should().Contain((stored, EntityState.Unchanged));
        save.Where(entry => entry.State == EntityState.Added)
            .Should()
            .ContainSingle()
            .Which.Address.Street.Should()
            .Be("Mill Lane");
        result.Addresses.Take(2).Should().AllSatisfy(link => link.Address.Should().BeSameAs(stored));
        result.Addresses.Take(2).Should().AllSatisfy(link => link.AddressId.Should().Be(stored.Id));
    }

    [Fact]
    public async Task CreateAsync_ShouldSetTheNavigationsTheResponseNeeds()
    {
        var organisation = NewOrganisation(Item([AddressType.Registered, AddressType.Principal]));

        var result = await Subject.CreateAsync(organisation, TestContext.Current.CancellationToken);

        var response = result.ToResponse().Organisation;
        response.OrganisationType.Should().Be("LTD");
        response
            .Addresses.Should()
            .ContainSingle()
            .Which.AddressTypes.Should()
            .Equal(AddressType.Registered, AddressType.Principal);
    }

    [Fact]
    public async Task CreateAsync_WhenAConcurrentRequestStoredTheAddress_ShouldRetryOnceWithAFreshLookup()
    {
        var organisation = NewOrganisation(Item([AddressType.Registered, AddressType.Principal]));
        var candidate = organisation.Addresses[0].Address;
        var stored = StoredAddress();
        Subject.StoredAddresses.Enqueue(null);
        Subject.StoredAddresses.Enqueue(stored);
        Context.Failures.Enqueue(UniqueViolation("ux_address_values"));

        var result = await Subject.CreateAsync(organisation, TestContext.Current.CancellationToken);

        Subject.AddressLookups.Should().Be(2);
        Context.Saves.Should().HaveCount(2);
        Context.Saves[0].Should().Equal((candidate, EntityState.Added));
        Context.Saves[1].Should().Equal((stored, EntityState.Unchanged));
        result.Addresses.Should().AllSatisfy(link => link.Address.Should().BeSameAs(stored));
        result
            .ToResponse()
            .Organisation.Addresses.Should()
            .ContainSingle()
            .Which.AddressTypes.Should()
            .Equal(AddressType.Registered, AddressType.Principal);
    }

    [Fact]
    public async Task CreateAsync_WhenRetrying_ShouldUnsetValidFromSoTheThreeTimestampsMatchAgain()
    {
        var organisation = NewOrganisation(Item([AddressType.Registered]));
        Subject.StoredAddresses.Enqueue(null);
        Subject.StoredAddresses.Enqueue(StoredAddress());
        Context.Failures.Enqueue(UniqueViolation("ux_address_values"));

        await Subject.CreateAsync(organisation, TestContext.Current.CancellationToken);

        // The fake stamps ValidFrom at the first save as TimestampInterceptor would. The retried
        // save must see it unset again on the organisation and its link, or the interceptor would
        // keep the failed attempt's ValidFrom beside a later CreatedAt and UpdatedAt
        Context.ValidFromAtSave.Should().HaveCount(2);
        Context.ValidFromAtSave.Should().AllSatisfy(seen => seen.Should().Equal(default(DateTime), default));
    }

    [Fact]
    public async Task CreateAsync_WhenTheRetryFailsToo_ShouldThrow()
    {
        var organisation = NewOrganisation(Item([AddressType.Registered]));
        Context.Failures.Enqueue(UniqueViolation("ux_address_values"));
        Context.Failures.Enqueue(UniqueViolation("ux_address_values"));

        var act = () => Subject.CreateAsync(organisation, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
        Subject.AddressLookups.Should().Be(2);
        Context.Saves.Should().HaveCount(2);
    }

    // Keyed by name, so the theory's data is a serialisable string and each row is listed on its
    // own (xUnit1045); the exception is built in the test
    private static readonly Dictionary<string, Func<DbUpdateException>> FailuresByName = new()
    {
        ["another unique index"] = () => UniqueViolation("ux_organisation_address_current"),
        ["another error on ux_address_values"] = () => Failure(PostgresErrorCodes.CheckViolation, "ux_address_values"),
        ["a foreign key"] = () => Failure(PostgresErrorCodes.ForeignKeyViolation, "fk_organisation_address_address"),
        ["not from Postgres"] = () => new DbUpdateException("Failed"),
    };

    public static TheoryData<string> OtherFailures => [.. FailuresByName.Keys];

    [Theory]
    [MemberData(nameof(OtherFailures))]
    public async Task CreateAsync_WhenAnythingElseFails_ShouldThrowWithoutRetrying(string failureName)
    {
        var failure = FailuresByName[failureName]();
        var organisation = NewOrganisation(Item([AddressType.Registered]));
        Context.Failures.Enqueue(failure);

        var act = () => Subject.CreateAsync(organisation, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().BeSameAs(failure);
        Subject.AddressLookups.Should().Be(1);
        Context.Saves.Should().ContainSingle();
    }

    [Fact]
    public async Task UsageTypeIdsAsync_ShouldGiveTheIdOfEveryKind()
    {
        var ids = await Subject.UsageTypeIdsAsync(TestContext.Current.CancellationToken);

        ids.Should().BeEquivalentTo(UsageTypeIds);
    }

    [Fact]
    public async Task UsageTypeIdsAsync_WhenAKindHasNoRow_ShouldThrow()
    {
        Subject.UsageTypeRows.RemoveAll(usageType => usageType.Name == "AUDIT");

        var act = () => Subject.UsageTypeIdsAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'AUDIT'*");
    }

    [Fact]
    public void GetAsync_ShouldReadOnlyLinksWithNoValidTo()
    {
        var sql = Subject.CurrentAddressesSql(Guid.NewGuid());

        sql.Split("valid_to IS NULL").Should().HaveCount(2);
    }

    private static Organisation NewOrganisation(params AddressRequest[] items) =>
        new OrganisationRequest
        {
            Name = "Acme Waste Ltd",
            CompaniesHouseNumber = "12345678",
            RegulatorNation = "GB-ENG",
            OrganisationType = "LTD",
            Addresses = [.. items],
        }.ToEntity(OrganisationTypeId, UsageTypeIds);

    private static AddressRequest Item(List<AddressType> addressTypes) =>
        new()
        {
            AddressTypes = addressTypes,
            BuildingNumber = "12A",
            Street = "High Street",
            Locality = "Testville",
            PostalCode = "AB1 2CD",
            CountryIso = "GB",
        };

    // A fresh instance per look-up, as a query returns
    private static Address StoredAddress() =>
        new()
        {
            Id = Guid.CreateVersion7(),
            BuildingNumber = "12A",
            Street = "High Street",
            Locality = "Testville",
            PostalCode = "AB1 2CD",
            CountryIso = "GB",
        };

    private static DbUpdateException UniqueViolation(string constraintName) =>
        Failure(PostgresErrorCodes.UniqueViolation, constraintName);

    private static DbUpdateException Failure(string sqlState, string constraintName) =>
        new(
            "An error occurred while saving the entity changes.",
            new PostgresException("Failed", "ERROR", "ERROR", sqlState, constraintName: constraintName)
        );

    private sealed class FakeContext()
        : PostgresDbContext(
            new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql("Host=127.0.0.1;Database=waste_organisations")
                .Options
        )
    {
        public Queue<DbUpdateException> Failures { get; } = new();

        /// <summary>The addresses tracked at each save, with their state.</summary>
        public List<List<(Address Address, EntityState State)>> Saves { get; } = [];

        /// <summary>
        /// The <see cref="IValidFrom.ValidFrom"/> of every Added organisation and link as each save
        /// saw it, before the fake stamps it as <c>TimestampInterceptor</c> would.
        /// </summary>
        public List<List<DateTime>> ValidFromAtSave { get; } = [];

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves.Add(ChangeTracker.Entries<Address>().Select(entry => (entry.Entity, entry.State)).ToList());

            // Stamped before the failure is thrown, as the interceptor runs before the failing SQL
            var validFrom = ChangeTracker
                .Entries<IValidFrom>()
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity)
                .ToList();
            ValidFromAtSave.Add(validFrom.Select(entity => entity.ValidFrom).ToList());
            foreach (var entity in validFrom.Where(entity => entity.ValidFrom == default))
                entity.ValidFrom = new DateTime(2026, 11, 4, 10, 15, 30, DateTimeKind.Utc);

            if (Failures.TryDequeue(out var failure))
                throw failure;

            return Task.FromResult(0);
        }
    }

    private sealed class TestService(FakeContext context)
        : PostgresOrganisationService(context, NullLogger<PostgresOrganisationService>.Instance)
    {
        public List<AddressUsageType> UsageTypeRows { get; } =
            UsageTypes.Select(usageType => new AddressUsageType { Id = usageType.Id, Name = usageType.Name }).ToList();

        /// <summary>What each address look-up finds, in turn; nothing once empty.</summary>
        public Queue<Address?> StoredAddresses { get; } = new();

        public int AddressLookups { get; private set; }

        public string CurrentAddressesSql(Guid id) => OrganisationWithCurrentAddresses(id).ToQueryString();

        protected override Task<OrganisationType?> FindOrganisationTypeAsync(
            Guid id,
            CancellationToken cancellationToken
        ) => Task.FromResult<OrganisationType?>(new OrganisationType { Id = id, Code = "LTD" });

        protected override Task<List<AddressUsageType>> LoadUsageTypesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(
                UsageTypeRows
                    .Select(usageType => new AddressUsageType { Id = usageType.Id, Name = usageType.Name })
                    .ToList()
            );

        protected override Task<Address?> FindAddressAsync(Address candidate, CancellationToken cancellationToken)
        {
            AddressLookups++;

            return Task.FromResult(StoredAddresses.TryDequeue(out var stored) ? stored : null);
        }
    }
}
