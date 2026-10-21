using System.Text.RegularExpressions;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data.Postgres;

namespace Defra.WasteOrganisations.Api.Tests.Data;

// The list is the published contract for organisationType: consumers build their labels from it,
// so every code must be distinct and every meaning must tell its code apart from the others
public class OrganisationTypeCodesTests
{
    [Fact]
    public void All_ShouldHoldTheCompaniesHouseTypesAndOurFour()
    {
        OrganisationTypeCodes.All.Should().HaveCount(40);
        OrganisationTypeCodes
            .All.Select(c => c.Code)
            .Should()
            .Contain(["ltd", "plc", "llp", "sole-trader", "partnership", "unincorporated-association", "public-body"]);
    }

    [Fact]
    public void Codes_ShouldBeUniqueAndInCompaniesHouseFormat()
    {
        var codes = OrganisationTypeCodes.All.Select(c => c.Code).ToList();

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().AllSatisfy(code => Regex.IsMatch(code, "^[a-z0-9]+(-[a-z0-9]+)*$").Should().BeTrue(code));
    }

    [Fact]
    public void Descriptions_ShouldBeUniqueAndNotEmpty()
    {
        var descriptions = OrganisationTypeCodes.All.Select(c => c.Description).ToList();

        descriptions.Should().OnlyHaveUniqueItems();
        descriptions.Should().AllSatisfy(d => d.Should().NotBeNullOrWhiteSpace());
    }
}
