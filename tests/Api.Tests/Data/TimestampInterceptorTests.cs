using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteOrganisations.Api.Tests.Data;

// Drives SetTimestamps against the change tracker with no database: SaveChanges itself is
// covered by Api.IntegrationTests
public class TimestampInterceptorTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 19, 9, 30, 15, 123, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(s_now.AddTicks(4567));

    [Fact]
    public void WhenAdded_ShouldSetUnsetTimestampsToNowInMilliseconds()
    {
        using var context = CreateContext();
        var organisation = NewOrganisation();
        var organisationAddress = new OrganisationAddress
        {
            Id = Guid.CreateVersion7(),
            UsageType = new AddressUsageType { Id = Guid.CreateVersion7(), Name = "Registered" },
            Address = new Address { Id = Guid.CreateVersion7() },
        };
        organisation.Addresses.Add(organisationAddress);
        context.Organisations.Add(organisation);

        Subject().SetTimestamps(context);

        var expected = s_now.UtcDateTime;
        organisation.ValidFrom.Should().Be(expected);
        organisation.CreatedAt.Should().Be(expected);
        organisation.UpdatedAt.Should().Be(expected);
        organisationAddress.ValidFrom.Should().Be(expected);
        organisationAddress.CreatedAt.Should().Be(expected);
        organisationAddress.UpdatedAt.Should().Be(expected);
        organisationAddress.Address.CreatedAt.Should().Be(expected);
        organisationAddress.Address.UpdatedAt.Should().Be(expected);
        expected.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void WhenAdded_ShouldOverwriteAuditTimestampsButKeepValidFrom()
    {
        using var context = CreateContext();
        var earlier = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var organisation = NewOrganisation();
        organisation.ValidFrom = earlier;
        organisation.CreatedAt = earlier;
        organisation.UpdatedAt = earlier;
        context.Organisations.Add(organisation);

        Subject().SetTimestamps(context);

        organisation.CreatedAt.Should().Be(s_now.UtcDateTime);
        organisation.UpdatedAt.Should().Be(s_now.UtcDateTime);
        organisation.ValidFrom.Should().Be(earlier);
    }

    [Fact]
    public void WhenModified_ShouldNotChangeCreatedAt()
    {
        using var context = CreateContext();
        var earlier = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var organisation = NewOrganisation();
        organisation.CreatedAt = earlier;
        organisation.UpdatedAt = earlier;
        context.Organisations.Attach(organisation);
        organisation.CreatedAt = earlier.AddYears(-10);
        organisation.TradingName = "Renamed";
        context.ChangeTracker.DetectChanges();

        Subject().SetTimestamps(context);

        organisation.CreatedAt.Should().Be(earlier);
        context.Entry(organisation).Property(o => o.CreatedAt).IsModified.Should().BeFalse();
    }

    // Update() marks every property modified and takes the entity's values as the originals, so
    // only leaving created_at out of the UPDATE keeps the stored value
    [Fact]
    public void WhenUpdatedWhileDetached_ShouldLeaveCreatedAtOutOfTheUpdate()
    {
        using var context = CreateContext();
        var organisation = NewOrganisation();
        organisation.CreatedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        context.Organisations.Update(organisation);

        Subject().SetTimestamps(context);

        var entry = context.Entry(organisation);
        entry.Property(o => o.CreatedAt).IsModified.Should().BeFalse();
        entry.Property(o => o.UpdatedAt).IsModified.Should().BeTrue();
        organisation.UpdatedAt.Should().Be(s_now.UtcDateTime);
    }

    [Fact]
    public void WhenModified_ShouldMoveOnlyUpdatedAt()
    {
        using var context = CreateContext();
        var earlier = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var organisation = NewOrganisation();
        organisation.ValidFrom = earlier;
        organisation.CreatedAt = earlier;
        organisation.UpdatedAt = earlier;
        context.Organisations.Attach(organisation);
        organisation.TradingName = "Renamed";
        context.ChangeTracker.DetectChanges();

        Subject().SetTimestamps(context);

        organisation.UpdatedAt.Should().Be(s_now.UtcDateTime);
        organisation.CreatedAt.Should().Be(earlier);
        organisation.ValidFrom.Should().Be(earlier);
        context.Entry(organisation).Property(o => o.UpdatedAt).IsModified.Should().BeTrue();
    }

    [Fact]
    public void WhenUnchanged_ShouldLeaveTimestamps()
    {
        using var context = CreateContext();
        var earlier = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var organisation = NewOrganisation();
        organisation.UpdatedAt = earlier;
        context.Organisations.Attach(organisation);

        Subject().SetTimestamps(context);

        organisation.UpdatedAt.Should().Be(earlier);
    }

    [Fact]
    public void WhenNoContext_ShouldDoNothing()
    {
        var act = () => Subject().SetTimestamps(null);

        act.Should().NotThrow();
    }

    // Nothing listens on port 1, so the save fails when it connects: the interceptor has
    // already run by then, which is what these two tests check
    [Fact]
    public void WhenSaving_ShouldSetTimestampsBeforeWriting()
    {
        using var context = CreateContext(Subject());
        var organisation = NewOrganisation();
        context.Organisations.Add(organisation);

        var act = () => context.SaveChanges();

        act.Should().Throw<Exception>();
        organisation.CreatedAt.Should().Be(s_now.UtcDateTime);
    }

    [Fact]
    public async Task WhenSavingAsync_ShouldSetTimestampsBeforeWriting()
    {
        await using var context = CreateContext(Subject());
        var organisation = NewOrganisation();
        context.Organisations.Add(organisation);

        var act = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Exception>();
        organisation.CreatedAt.Should().Be(s_now.UtcDateTime);
    }

    private TimestampInterceptor Subject() => new(_timeProvider);

    private static Organisation NewOrganisation() =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Name = "Organisation",
            HomeNation = "GB-ENG",
        };

    private static PostgresDbContext CreateContext(TimestampInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(
            "Host=127.0.0.1;Port=1;Database=waste_organisations;Timeout=1"
        );
        if (interceptor is not null)
            options.AddInterceptors(interceptor);

        return new PostgresDbContext(options.Options);
    }
}
