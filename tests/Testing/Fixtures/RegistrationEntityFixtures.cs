using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteOrganisations.Api;
using Defra.WasteOrganisations.Api.Data.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using EnumExtensions = Defra.WasteOrganisations.Testing.Extensions.EnumExtensions;
using Registration = Defra.WasteOrganisations.Api.Data.Entities.Registration;

// ReSharper disable ConvertClosureToMethodGroup

namespace Defra.WasteOrganisations.Testing.Fixtures;

public static class RegistrationEntityFixtures
{
    private static Fixture GetFixture() => new();

    private static int RandomRegistrationYear() =>
        Random.Shared.Next(RegistrationYear.Minimum, RegistrationYear.Maximum + 1);

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

    public static Dictionary<RegistrationKey, Registration> CreateAsDictionary(
        this IPostprocessComposer<Registration> composer
    )
    {
        var registration = composer.Create();

        return new Dictionary<RegistrationKey, Registration> { { registration.Key, registration } };
    }
}
