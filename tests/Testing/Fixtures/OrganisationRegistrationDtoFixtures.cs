using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteOrganisations.Api.Dtos;

namespace Defra.WasteOrganisations.Testing.Fixtures;

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
            .With(x => x.Name, "Test Name Ltd")
            .With(x => x.TradingName, "Trading Name")
            .With(x => x.BusinessCountry, BusinessCountry.England)
            .With(x => x.CompaniesHouseNumber, "12345678")
            .With(x => x.Address, AddressDtoFixtures.Default().Create())
            .With(x => x.Registration, RegistrationDtoFixtures.Default().Create());
    }

    public static IPostprocessComposer<OrganisationRegistration> LargeProducer()
    {
        return Default()
            .With(x => x.Name, "Large Producer Ltd")
            .With(x => x.TradingName, "Large Producer Trading")
            .With(x => x.Registration, RegistrationDtoFixtures.LargeProducer().Create());
    }

    public static IPostprocessComposer<OrganisationRegistration> ComplianceScheme()
    {
        return Default()
            .With(x => x.Name, "Compliance Scheme Ltd")
            .With(x => x.TradingName, "Compliance Scheme Trading")
            .With(x => x.Registration, RegistrationDtoFixtures.ComplianceScheme().Create());
    }
}
