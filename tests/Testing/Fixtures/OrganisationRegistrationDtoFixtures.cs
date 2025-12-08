using Api.Dtos;
using AutoFixture;
using AutoFixture.Dsl;

namespace Testing.Fixtures;

public static class OrganisationRegistrationDtoFixtures
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<OrganisationRegistration> Organisation()
    {
        return GetFixture().Build<OrganisationRegistration>();
    }

    public static IPostprocessComposer<OrganisationRegistration> Default()
    {
        return Organisation()
            .With(x => x.Name, "England Ltd")
            .With(x => x.TradingName, "Trading Name")
            .With(x => x.BusinessCountry, BusinessCountry.England)
            .With(x => x.CompaniesHouseNumber, "12345678")
            .With(x => x.Address, AddressDtoFixtures.Default().Create())
            .With(x => x.Registration, RegistrationDtoFixtures.Default().Create());
    }
}
