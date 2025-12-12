using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteOrganisations.Api.Dtos;

namespace Defra.WasteOrganisations.Testing.Fixtures;

public static class RegistrationRequestDtoFixtures
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<RegistrationRequest> RegistrationRequest()
    {
        return GetFixture().Build<RegistrationRequest>();
    }

    public static IPostprocessComposer<RegistrationRequest> Default()
    {
        return RegistrationRequest().With(x => x.Status, RegistrationStatus.Registered);
    }
}
