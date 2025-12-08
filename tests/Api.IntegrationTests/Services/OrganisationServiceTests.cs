using Api.Data;
using Api.Services;
using AutoFixture;
using AwesomeAssertions;
using Testing.Extensions;
using Testing.Fixtures;

namespace Api.IntegrationTests.Services;

public class OrganisationServiceTests : IntegrationTestBase
{
    private OrganisationService Subject { get; } = new(new MongoDbContext(GetMongoDatabase()));

    [Fact]
    public async Task Get_WhenNoOrganisation_ShouldBeNull()
    {
        var organisation = await Subject.Get(Guid.NewGuid(), TestContext.Current.CancellationToken);

        organisation.Should().BeNull();
    }

    [Fact]
    public async Task Create_WhenInserted_ShouldBeFound()
    {
        var initial = await Subject.Create(
            OrganisationEntityFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var retrieved = await Subject.Get(initial.Id, TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(initial, options => options.AllowMongoDateTimePrecision());
    }

    [Fact]
    public async Task Update_WhenUpdated_ShouldChange()
    {
        var initial = await Subject.Create(
            OrganisationEntityFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );
        initial.Version.Should().Be(1);
        initial.Created.Should().Be(initial.Updated).And.NotBe(DateTime.MinValue);

        var retrieved = await Subject.Get(initial.Id, TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved = retrieved with { TradingName = "Changed Trading Name" };

        retrieved = await Subject.Update(retrieved, TestContext.Current.CancellationToken);
        retrieved.Version.Should().Be(2);
        retrieved.Updated.Should().BeAfter(retrieved.Created);

        retrieved.TradingName.Should().Be("Changed Trading Name");

        retrieved = await Subject.Get(initial.Id, TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved.TradingName.Should().Be("Changed Trading Name");
    }

    [Fact]
    public async Task Update_WhenConcurrent_SecondShouldFail()
    {
        var initial = await Subject.Create(
            OrganisationEntityFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var retrieved1 = await Subject.Get(initial.Id, TestContext.Current.CancellationToken);
        var retrieved2 = await Subject.Get(initial.Id, TestContext.Current.CancellationToken);

        retrieved1.Should().NotBeNull();
        retrieved2.Should().NotBeNull();

        retrieved1 = retrieved1 with { TradingName = "Changed Trading Name 1" };
        await Subject.Update(retrieved1, TestContext.Current.CancellationToken);

        retrieved2 = retrieved2 with { TradingName = "Changed Trading Name 2" };
        var act = async () => await Subject.Update(retrieved2, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<ConcurrencyException>()
            .WithMessage("Concurrency issue on write, organisation was not updated");
    }
}
