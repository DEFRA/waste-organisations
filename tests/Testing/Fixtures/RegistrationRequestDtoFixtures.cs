using Api.Dtos;
using AutoFixture;
using AutoFixture.Dsl;

namespace Testing.Fixtures;

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
