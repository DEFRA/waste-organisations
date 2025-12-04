using Api.Dtos;
using AutoFixture;
using AutoFixture.Dsl;

namespace Testing.Fixtures;

public static class RegistrationDtoFixtures
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Registration> Registration()
    {
        return GetFixture().Build<Registration>();
    }

    public static IPostprocessComposer<Registration> Default()
    {
        return Registration()
            .With(x => x.Type, RegistrationType.SmallProducer)
            .With(x => x.RegistrationYear, 2025)
            .With(x => x.Status, RegistrationStatus.Registered);
    }
}
