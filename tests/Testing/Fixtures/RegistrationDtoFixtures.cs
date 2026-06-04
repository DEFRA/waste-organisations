using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteOrganisations.Api.Dtos;

namespace Defra.WasteOrganisations.Testing.Fixtures;

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

    public static IPostprocessComposer<Registration> LargeProducer()
    {
        return Default().With(x => x.Type, RegistrationType.LargeProducer);
    }

    public static IPostprocessComposer<Registration> ComplianceScheme()
    {
        return Default().With(x => x.Type, RegistrationType.ComplianceScheme);
    }
}
