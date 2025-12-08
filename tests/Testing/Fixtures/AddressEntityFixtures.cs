using Api.Data.Entities;
using AutoFixture;
using AutoFixture.Dsl;

namespace Testing.Fixtures;

public static class AddressEntityFixtures
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Address> Address()
    {
        return GetFixture().Build<Address>();
    }

    public static IPostprocessComposer<Address> Default()
    {
        return Address()
            .With(x => x.AddressLine1, "England Ltd")
            .With(x => x.AddressLine2, "123 Street")
            .With(x => x.Town, "Town")
            .With(x => x.County, "County")
            .With(x => x.Postcode, "UK1")
            .With(x => x.Country, "UK");
    }
}
