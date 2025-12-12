using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Testing.Fixtures;

namespace Defra.WasteOrganisations.Api.Tests.Mapping;

public class OrganisationExtensionsTests
{
    [Fact]
    public void Patch_WhenNoChange_ShouldBeTheSame()
    {
        var organisation = OrganisationEntityFixtures.Default().Create();
        var request = OrganisationRegistrationDtoFixtures.Default().Create();

        organisation = organisation.Patch(request);

        organisation.Registrations.Should().BeEquivalentTo([RegistrationEntityFixtures.Default().Create()]);
    }

    [Fact]
    public void Patch_WhenYearChange_ShouldAddNewRegistration()
    {
        var organisation = OrganisationEntityFixtures.Default().Create();
        var request = OrganisationRegistrationDtoFixtures
            .Default()
            .With(x => x.Registration, RegistrationDtoFixtures.Default().With(x => x.RegistrationYear, 2026).Create())
            .Create();

        organisation = organisation.Patch(request);

        organisation
            .Registrations.Should()
            .BeEquivalentTo([
                RegistrationEntityFixtures.Default().Create(),
                RegistrationEntityFixtures.Default().With(x => x.RegistrationYear, 2026).Create(),
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

        organisation = organisation.Patch(request);

        organisation
            .Registrations.Should()
            .BeEquivalentTo([
                RegistrationEntityFixtures.Default().Create(),
                RegistrationEntityFixtures
                    .Default()
                    .With(x => x.Type, RegistrationType.LargeProducer.ToJsonValue())
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

        organisation = organisation.Patch(request);

        organisation
            .Registrations.Should()
            .BeEquivalentTo([
                RegistrationEntityFixtures
                    .Default()
                    .With(x => x.Status, RegistrationStatus.Cancelled.ToJsonValue())
                    .Create(),
            ]);
    }
}
