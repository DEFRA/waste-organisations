using Api;
using Api.Dtos;
using Api.Extensions;
using AutoFixture;
using AutoFixture.Dsl;
using EnumExtensions = Testing.Extensions.EnumExtensions;
using Registration = Api.Data.Entities.Registration;

// ReSharper disable ConvertClosureToMethodGroup

namespace Testing.Fixtures;

public static class RegistrationEntityFixtures
{
    private static Fixture GetFixture() => new();

    private static int RandomRegistrationYear() =>
        Random.Shared.Next(RegistrationYear.Minimum, RegistrationYear.Maximum);

    public static void ConfigureDefaults(Fixture fixture)
    {
        fixture.Customize<Registration>(x =>
            x.With(y => y.Type, () => EnumExtensions.RandomJsonValue<RegistrationType>())
                .With(y => y.RegistrationYear, () => RandomRegistrationYear())
                .With(y => y.Status, () => EnumExtensions.RandomJsonValue<RegistrationStatus>())
        );
    }

    public static IPostprocessComposer<Registration> Registration()
    {
        var fixture = GetFixture();

        ConfigureDefaults(fixture);

        return fixture
            .Build<Registration>()
            .With(x => x.Type, () => EnumExtensions.RandomJsonValue<RegistrationType>())
            .With(x => x.RegistrationYear, () => RandomRegistrationYear())
            .With(x => x.Status, () => EnumExtensions.RandomJsonValue<RegistrationStatus>());
    }

    public static IPostprocessComposer<Registration> Default()
    {
        return Registration()
            .With(x => x.Type, RegistrationType.SmallProducer.ToJsonValue())
            .With(x => x.RegistrationYear, 2025)
            .With(x => x.Status, RegistrationStatus.Registered.ToJsonValue());
    }
}
