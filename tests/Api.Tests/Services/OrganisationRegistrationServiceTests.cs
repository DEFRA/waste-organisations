using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Testing.Fixtures;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteOrganisations.Api.Tests.Services;

public class OrganisationRegistrationServiceTests
{
    private FakeTimeProvider TimeProvider { get; }
    private Func<DateTime> UtcNow => () => TimeProvider.GetUtcNow().UtcDateTime;
    private OrganisationRegistrationService Subject { get; }

    public OrganisationRegistrationServiceTests()
    {
        TimeProvider = new FakeTimeProvider();
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 4, 30, 13, 40, 0, TimeSpan.Zero));

        Subject = new OrganisationRegistrationService(TimeProvider);
    }

    [Fact]
    public void Patch_WhenNoChange_ShouldBeTheSame()
    {
        var organisation = OrganisationEntityFixtures.Default().Create();
        var request = OrganisationRegistrationDtoFixtures.Default().Create();

        organisation = Subject.Patch(organisation, request);

        organisation
            .Registrations.Should()
            .BeEquivalentTo([RegistrationEntityFixtures.Default().With(x => x.Updated, UtcNow()).Create()]);
    }

    [Fact]
    public void Patch_WhenYearChange_ShouldAddNewRegistration()
    {
        var organisation = OrganisationEntityFixtures.Default().Create();
        var request = OrganisationRegistrationDtoFixtures
            .Default()
            .With(x => x.Registration, RegistrationDtoFixtures.Default().With(x => x.RegistrationYear, 2026).Create())
            .Create();

        organisation = Subject.Patch(organisation, request);

        organisation
            .Registrations.Should()
            .BeEquivalentTo([
                RegistrationEntityFixtures.Default().Create(),
                RegistrationEntityFixtures
                    .Default()
                    .With(x => x.RegistrationYear, 2026)
                    .With(x => x.Created, UtcNow())
                    .With(x => x.Updated, UtcNow())
                    .Create(),
            ]);
    }

    [Fact]
    public void Patch_WhenTypeChange_ShouldAddNewRegistration()
    {
        var organisation = OrganisationEntityFixtures.Default().Create();
        var request = OrganisationRegistrationDtoFixtures
            .Default()
            .With(
                x => x.Registration,
                RegistrationDtoFixtures.Default().With(x => x.Type, RegistrationType.LargeProducer).Create()
            )
            .Create();

        organisation = Subject.Patch(organisation, request);

        organisation
            .Registrations.Should()
            .BeEquivalentTo([
                RegistrationEntityFixtures.Default().Create(),
                RegistrationEntityFixtures
                    .Default()
                    .With(x => x.Type, RegistrationType.LargeProducer.ToJsonValue())
                    .With(x => x.Created, UtcNow())
                    .With(x => x.Updated, UtcNow())
                    .Create(),
            ]);
    }

    [Fact]
    public void Patch_WhenStatusChange_ShouldUpdateStatus()
    {
        var organisation = OrganisationEntityFixtures.Default().Create();
        var request = OrganisationRegistrationDtoFixtures
            .Default()
            .With(
                x => x.Registration,
                RegistrationDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Cancelled).Create()
            )
            .Create();

        organisation = Subject.Patch(organisation, request);

        organisation
            .Registrations.Should()
            .BeEquivalentTo([
                RegistrationEntityFixtures
                    .Default()
                    .With(x => x.Status, RegistrationStatus.Cancelled.ToJsonValue())
                    .With(x => x.Updated, UtcNow())
                    .Create(),
            ]);
    }
}
