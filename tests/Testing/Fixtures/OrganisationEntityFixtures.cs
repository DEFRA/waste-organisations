using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using EnumExtensions = Defra.WasteOrganisations.Testing.Extensions.EnumExtensions;
using Organisation = Defra.WasteOrganisations.Api.Data.Entities.Organisation;

// ReSharper disable ConvertClosureToMethodGroup

namespace Defra.WasteOrganisations.Testing.Fixtures;

public static class OrganisationEntityFixtures
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Organisation> Organisation()
    {
        var fixture = GetFixture();

        RegistrationEntityFixtures.ConfigureDefaults(fixture);

        return fixture
            .Build<Organisation>()
            .With(x => x.BusinessCountry, () => EnumExtensions.RandomJsonValue<BusinessCountry>());
    }

    public static IPostprocessComposer<Organisation> Default()
    {
        return Organisation()
            .With(x => x.Name, "Test Name Ltd")
            .With(x => x.TradingName, "Trading Name")
            .With(x => x.BusinessCountry, BusinessCountry.England.ToJsonValue())
            .With(x => x.CompaniesHouseNumber, "12345678")
            .With(x => x.Address, AddressEntityFixtures.Default().Create())
            .With(x => x.Registrations, () => RegistrationEntityFixtures.Default().CreateAsDictionary());
    }
}
