using System.Text.RegularExpressions;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data.Postgres;

namespace Defra.WasteOrganisations.Api.Tests.Data;

// The list is the published contract for organisationType: consumers build their labels from it,
// so every code must be distinct and every meaning must tell its code apart from the others
public partial class OrganisationTypeCodesTests
{
    [Fact]
    public void All_ShouldHoldTheTenConfluenceTypes()
    {
        OrganisationTypeCodes
            .All.Select(c => c.Code)
            .Should()
            .Equal(
                "LTD",
                "PLC",
                "LIMITED_PARTNERSHIP",
                "LLP",
                "CIC",
                "SOLE_TRADER",
                "PARTNERSHIP",
                "REGISTERED_SOCIETY",
                "CHARITABLE_ORGANISATION",
                "OVERSEAS_ORGANISATION"
            );
    }

    // The name, Companies House number and address kind rules all read this flag, so a wrong flag
    // would require or forbid a Companies House number for the wrong type
    [Fact]
    public void RegisteredAtCompaniesHouse_ShouldBeTrueForTheFiveCompaniesHouseTypesOnly()
    {
        OrganisationTypeCodes
            .All.Where(c => c.RegisteredAtCompaniesHouse)
            .Select(c => c.Code)
            .Should()
            .BeEquivalentTo("LTD", "PLC", "LIMITED_PARTNERSHIP", "LLP", "CIC");
    }

    [Fact]
    public void Codes_ShouldBeUniqueAndInUpperSnakeCase()
    {
        var codes = OrganisationTypeCodes.All.Select(c => c.Code).ToList();

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().AllSatisfy(code => UpperSnakeCase().IsMatch(code).Should().BeTrue(code));
    }

    [Fact]
    public void Descriptions_ShouldBeUniqueAndNotEmpty()
    {
        var descriptions = OrganisationTypeCodes.All.Select(c => c.Description).ToList();

        descriptions.Should().OnlyHaveUniqueItems();
        descriptions.Should().AllSatisfy(d => d.Should().NotBeNullOrWhiteSpace());
    }

    // Upper-case words joined by single underscores, the form addressTypes uses on the wire too
    [GeneratedRegex("^[A-Z0-9]+(_[A-Z0-9]+)*$")]
    private static partial Regex UpperSnakeCase();
}
