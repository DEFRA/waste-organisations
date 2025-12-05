using Api.Dtos;
using Api.Extensions;
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
        var values = Enum.GetValues<RegistrationType>();

        return values[Random.Shared.Next(0, values.Length)].ToJsonValue();
    }

    private static string RandomStatus()
    {
        var values = Enum.GetValues<RegistrationStatus>();

        return values[Random.Shared.Next(0, values.Length)].ToJsonValue();
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
            .With(x => x.Type, RegistrationType.SmallProducer.ToJsonValue())
            .With(x => x.RegistrationYear, 2025)
            .With(x => x.Status, RegistrationStatus.Registered.ToJsonValue());
    }
}
