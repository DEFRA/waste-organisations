using Api.Dtos;
using Api.Extensions;
using AutoFixture;
using AutoFixture.Dsl;
using Organisation = Api.Data.Entities.Organisation;

namespace Testing.Fixtures;

public static class OrganisationEntityFixtures
{
    private static Fixture GetFixture() => new();

    private static string RandomBusinessCountry()
    {
        var values = Enum.GetValues<BusinessCountry>();

        return values[Random.Shared.Next(0, values.Length)].ToJsonValue();
    }

    public static IPostprocessComposer<Organisation> Organisation()
    {
        var fixture = GetFixture();

        RegistrationEntityFixtures.ConfigureDefaults(fixture);

        return fixture.Build<Organisation>().With(x => x.BusinessCountry, RandomBusinessCountry());
    }

    public static IPostprocessComposer<Organisation> Default()
    {
        return Organisation()
            .With(x => x.Name, "England Ltd")
            .With(x => x.TradingName, "Trading Name")
            .With(x => x.BusinessCountry, BusinessCountry.England.ToJsonValue())
            .With(x => x.CompaniesHouseNumber, "12345678")
            .With(x => x.Address, AddressEntityFixtures.Default().Create())
            .With(x => x.Registrations, () => [RegistrationEntityFixtures.Default().Create()]);
    }
}
