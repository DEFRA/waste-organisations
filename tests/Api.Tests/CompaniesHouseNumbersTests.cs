using AwesomeAssertions;
using Defra.WasteOrganisations.Testing;

namespace Defra.WasteOrganisations.Api.Tests;

public class CompaniesHouseNumbersTests
{
    private const string AcceptedFormat = "^[A-Z0-9]{2}[0-9]{6}$";

    [Fact]
    public void Generate_ShouldGiveEightDigits()
    {
        var numbers = Enumerable.Range(0, 100).Select(_ => CompaniesHouseNumbers.Generate()).ToArray();

        numbers.Should().AllSatisfy(number => number.Should().MatchRegex("^[0-9]{8}$"));
    }

    [Fact]
    public void Generate_ShouldNotRepeatANumberOrGiveTheFixedOne()
    {
        var numbers = Enumerable
            .Range(0, 1_000)
            .Select(_ => CompaniesHouseNumbers.Generate())
            .Concat(Enumerable.Range(0, 1_000).Select(_ => CompaniesHouseNumbers.Generate("SC")))
            .ToArray();

        numbers.Should().OnlyHaveUniqueItems().And.NotContain(CompaniesHouseNumbers.FullPayload);
    }

    [Fact]
    public void Generate_WithPrefix_ShouldGiveThePrefixAndSixDigits()
    {
        var numbers = Enumerable.Range(0, 100).Select(_ => CompaniesHouseNumbers.Generate("SC")).ToArray();

        numbers
            .Should()
            .AllSatisfy(number =>
            {
                number.Should().MatchRegex("^SC[0-9]{6}$");
                number.Should().MatchRegex(AcceptedFormat);
            });
    }

    [Theory]
    [InlineData("sc")]
    [InlineData("S")]
    [InlineData("SCO")]
    [InlineData("S1")]
    [InlineData("")]
    public void Generate_WithPrefixNotTwoUpperCaseLetters_ShouldThrow(string prefix)
    {
        var generate = () => CompaniesHouseNumbers.Generate(prefix);

        generate.Should().Throw<ArgumentException>().WithParameterName(nameof(prefix));
    }

    [Fact]
    public void FullPayload_ShouldMatchTheAcceptedFormat()
    {
        CompaniesHouseNumbers.FullPayload.Should().MatchRegex(AcceptedFormat);
    }
}
