using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Testing.Extensions;
using Defra.WasteOrganisations.Testing.Fixtures;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Services;

public class OrganisationServiceTests : MongoTestBase
{
    private OrganisationService Subject { get; } =
        new(new MongoDbContext(GetMongoDatabase()), Substitute.For<ILogger<OrganisationService>>());

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

    [Fact]
    public async Task Search_WhenRegistrationTypeMatch_ShouldBeFound()
    {
        var initial = await Subject.Create(
            OrganisationEntityFixtures
                .Default()
                .With(
                    x => x.Registrations,
                    RegistrationEntityFixtures
                        .Default()
                        .With(x => x.Type, RegistrationType.LargeProducer.ToJsonValue())
                        .CreateAsDictionary()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        var results = await Subject.Search(
            [RegistrationType.LargeProducer],
            [],
            [],
            TestContext.Current.CancellationToken
        );

        results.Should().ContainSingle();
        results.Should().BeEquivalentTo([initial], options => options.AllowMongoDateTimePrecision());
    }

    [Theory]
    [InlineData(2025)]
    [InlineData(2026)]
    public async Task Search_WhenRegistrationYearMatch_ShouldBeFound(int registrationYear)
    {
        var initial = await Subject.Create(
            OrganisationEntityFixtures
                .Default()
                .With(
                    x => x.Registrations,
                    RegistrationEntityFixtures
                        .Default()
                        .With(x => x.RegistrationYear, registrationYear)
                        .CreateAsDictionary()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        var results = await Subject.Search([], [registrationYear], [], TestContext.Current.CancellationToken);

        results.Should().ContainSingle();
        results.Should().BeEquivalentTo([initial], options => options.AllowMongoDateTimePrecision());
    }

    [Fact]
    public async Task Search_WhenRegistrationStatusMatch_ShouldBeFound()
    {
        var initial = await Subject.Create(
            OrganisationEntityFixtures
                .Default()
                .With(
                    x => x.Registrations,
                    RegistrationEntityFixtures
                        .Default()
                        .With(x => x.Status, RegistrationStatus.Registered.ToJsonValue())
                        .CreateAsDictionary()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        var results = await Subject.Search(
            [],
            [],
            [RegistrationStatus.Registered],
            TestContext.Current.CancellationToken
        );

        results.Should().ContainSingle();
        results.Should().BeEquivalentTo([initial], options => options.AllowMongoDateTimePrecision());
    }

    [Fact]
    public async Task Search_WhenMultipleRegistrationFieldsMatch_ShouldBeFound()
    {
        var initial = await Subject.Create(
            OrganisationEntityFixtures
                .Default()
                .With(
                    x => x.Registrations,
                    RegistrationEntityFixtures
                        .Default()
                        .With(x => x.Type, RegistrationType.LargeProducer.ToJsonValue())
                        .With(x => x.RegistrationYear, 2025)
                        .With(x => x.Status, RegistrationStatus.Registered.ToJsonValue())
                        .CreateAsDictionary()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        var results = await Subject.Search(
            [RegistrationType.LargeProducer],
            [2025],
            [RegistrationStatus.Registered],
            TestContext.Current.CancellationToken
        );

        results.Should().ContainSingle();
        results.Should().BeEquivalentTo([initial], options => options.AllowMongoDateTimePrecision());
    }
}
