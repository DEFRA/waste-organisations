using AutoFixture;
using AwesomeAssertions;
using Testing.Fixtures;

namespace Api.Tests;

public class FixturesTests
{
    [Fact]
    public void OrganisationEntity_ShouldCreate()
    {
        var organisation = OrganisationEntityFixtures.Organisation().Create();

        organisation.Should().NotBeNull();
    }

    [Fact]
    public void RegistrationEntity_ShouldCreate()
    {
        var registration = RegistrationEntityFixtures.Registration().Create();

        registration.Should().NotBeNull();
    }
}
