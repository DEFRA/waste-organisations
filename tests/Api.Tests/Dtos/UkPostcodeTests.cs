using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos.Validation;

namespace Defra.WasteOrganisations.Api.Tests.Dtos;

public class UkPostcodeTests
{
    [Theory]
    [InlineData("ab1-2cd", "AB1 2CD")]
    [InlineData(" AB12CD ", "AB1 2CD")]
    [InlineData("ab1   2cd", "AB1 2CD")]
    [InlineData("(ab1) 2cd.", "AB1 2CD")]
    [InlineData("A1 2BC", "A1 2BC")]
    [InlineData("A12 3BC", "A12 3BC")]
    [InlineData("AB1 2CD", "AB1 2CD")]
    [InlineData("AB12 3CD", "AB12 3CD")]
    [InlineData("A1B 2CD", "A1B 2CD")]
    [InlineData("AB1C 2DE", "AB1C 2DE")]
    [InlineData("GIR 0AA", "GIR 0AA")]
    public void TryNormalise_WithAFullUkPostcode_ShouldGiveTheStoredForm(string value, string expected)
    {
        UkPostcode.TryNormalise(value, out var postcode).Should().BeTrue();

        postcode.Should().Be(expected);
    }

    [Theory]
    [InlineData("AB1")]
    [InlineData("12345")]
    [InlineData("AB1 2CDE")]
    [InlineData("ABC 1DE")]
    [InlineData("AB1 ACD")]
    [InlineData("---")]
    [InlineData("")]
    [InlineData(null)]
    public void TryNormalise_WithoutAFullUkPostcode_ShouldGiveNull(string? value)
    {
        UkPostcode.TryNormalise(value, out var postcode).Should().BeFalse();

        postcode.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("( ).")]
    public void IsBlank_WithNoLetterOrDigit_ShouldBeTrue(string? value)
    {
        UkPostcode.IsBlank(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("AB1")]
    [InlineData("-1-")]
    [InlineData("AB1 2CD")]
    public void IsBlank_WithALetterOrDigit_ShouldBeFalse(string value)
    {
        UkPostcode.IsBlank(value).Should().BeFalse();
    }
}
