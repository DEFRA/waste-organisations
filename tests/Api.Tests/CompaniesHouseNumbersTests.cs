using System.Reflection;
using AwesomeAssertions;
using Defra.WasteOrganisations.Testing;

namespace Defra.WasteOrganisations.Api.Tests;

public class CompaniesHouseNumbersTests
{
    private static readonly string[] Constants = typeof(CompaniesHouseNumbers)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void Constants_ShouldBeDistinct()
    {
        Constants.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Constants_ShouldMatchTheAcceptedFormat()
    {
        Constants.Should().NotBeEmpty().And.AllSatisfy(number => number.Should().MatchRegex("^[A-Z0-9]{2}[0-9]{6}$"));
    }
}
