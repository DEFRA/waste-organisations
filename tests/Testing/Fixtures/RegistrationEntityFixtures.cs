using Api.Dtos;
using AutoFixture;
using AutoFixture.Dsl;
using Registration = Api.Data.Entities.Registration;

namespace Testing.Fixtures;

public static class RegistrationEntityFixtures
{
    private static Fixture GetFixture() => new();

    public static void ConfigureDefaults(Fixture fixture)
    {
        fixture.Customize<Registration>(x => x.With(y => y.Type, RandomType()).With(y => y.Status, RandomStatus()));
    }

    private static string RandomType()
    {
        var names = Enum.GetNames<RegistrationType>();

        return names[Random.Shared.Next(0, names.Length)];
    }

    private static string RandomStatus()
    {
        var names = Enum.GetNames<RegistrationStatus>();

        return names[Random.Shared.Next(0, names.Length)];
    }

    public static IPostprocessComposer<Registration> Registration()
    {
        var fixture = GetFixture();

        ConfigureDefaults(fixture);

        return fixture.Build<Registration>().With(x => x.Type, RandomType()).With(x => x.Status, RandomStatus());
    }

    public static IPostprocessComposer<Registration> Default()
    {
        return Registration()
            .With(x => x.Type, nameof(RegistrationType.SmallProducer))
            .With(x => x.RegistrationYear, 2025)
            .With(x => x.Status, nameof(RegistrationStatus.Registered));
    }
}
