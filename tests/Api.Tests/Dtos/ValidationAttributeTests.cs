using System.ComponentModel.DataAnnotations;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data.Postgres;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Dtos.Attributes;

namespace Defra.WasteOrganisations.Api.Tests.Dtos;

public class ValidationAttributeTests
{
    public static TheoryData<string> TypeCodes => [.. OrganisationTypeCodes.All.Select(c => c.Code)];

    [Theory]
    [MemberData(nameof(TypeCodes))]
    public void OrganisationTypeCode_ShouldAcceptEveryPublishedCode(string code)
    {
        new OrganisationTypeCodeAttribute().IsValid(code).Should().BeTrue();
    }

    // Codes are matched exactly as published, so the 1.1 kebab-case codes are rejected
    [Theory]
    [InlineData("ltd")]
    [InlineData("UNKNOWN")]
    [InlineData("sole-trader")]
    [InlineData("")]
    public void OrganisationTypeCode_ShouldRejectAnythingElse(string code)
    {
        new OrganisationTypeCodeAttribute().IsValid(code).Should().BeFalse();
    }

    // Every string is trimmed before validation, so surrounding spaces are not an error
    [Fact]
    public void OrganisationTypeCode_ShouldAcceptAPaddedCode()
    {
        new OrganisationTypeCodeAttribute().IsValid(" LTD ").Should().BeTrue();
    }

    [Fact]
    public void OrganisationTypeCode_ShouldNameTheCodesInItsMessage()
    {
        new OrganisationTypeCodeAttribute()
            .FormatErrorMessage("organisationType")
            .Should()
            .Be(
                "The organisationType field must be one of LTD, PLC, LIMITED_PARTNERSHIP, LLP, CIC, SOLE_TRADER, "
                    + "PARTNERSHIP, REGISTERED_SOCIETY, CHARITABLE_ORGANISATION, OVERSEAS_ORGANISATION."
            );
    }

    [Theory]
    [InlineData("GB-ENG")]
    [InlineData("GB-SCT")]
    [InlineData("GB-WLS")]
    [InlineData("GB-NIR")]
    [InlineData(" GB-ENG ")]
    public void JsonEnumValue_ShouldAcceptTheWireNames(string value)
    {
        new JsonEnumValueAttribute<RegulatorNation>().IsValid(value).Should().BeTrue();
    }

    // JsonStringEnumConverter would accept other casings, member names and numbers; the contract
    // is the four codes as written
    [Theory]
    [InlineData("GB-XXX")]
    [InlineData("gb-eng")]
    [InlineData("England")]
    [InlineData("0")]
    [InlineData("")]
    public void JsonEnumValue_ShouldRejectAnythingElse(string value)
    {
        new JsonEnumValueAttribute<RegulatorNation>().IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void JsonEnumValue_ShouldNameTheValuesInItsMessage()
    {
        new JsonEnumValueAttribute<RegulatorNation>()
            .FormatErrorMessage("regulatorNation")
            .Should()
            .Be("The regulatorNation field must be one of GB-ENG, GB-SCT, GB-WLS, GB-NIR.");
    }

    // A missing value is reported once, by [Required], not again by the value check
    [Fact]
    public void MissingRegulatorNationAndType_ShouldEachBeReportedOnceAsRequired()
    {
        var request = new OrganisationRequest { Addresses = [] };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        results
            .Where(r => r.MemberNames.Contains(nameof(OrganisationRequest.RegulatorNation)))
            .Should()
            .ContainSingle()
            .Which.ErrorMessage.Should()
            .Contain("required");
        results
            .Where(r => r.MemberNames.Contains(nameof(OrganisationRequest.OrganisationType)))
            .Should()
            .ContainSingle()
            .Which.ErrorMessage.Should()
            .Contain("required");
    }

    [Fact]
    public void Addresses_ShouldHoldOneToFiveItems()
    {
        Validate(new OrganisationRequest { Addresses = [] }).Should().Contain(nameof(OrganisationRequest.Addresses));
        Validate(new OrganisationRequest { Addresses = [.. Enumerable.Repeat(new AddressRequest(), 6)] })
            .Should()
            .Contain(nameof(OrganisationRequest.Addresses));
        Validate(new OrganisationRequest { Addresses = [.. Enumerable.Repeat(new AddressRequest(), 5)] })
            .Should()
            .NotContain(nameof(OrganisationRequest.Addresses));
    }

    // [Required] treats a whitespace-only string as missing, so a blank street is an error
    [Fact]
    public void Address_ShouldRequireTypesStreetLocalityAndCountry()
    {
        Validate(new AddressRequest { AddressTypes = [], Street = " " })
            .Should()
            .BeEquivalentTo(
                nameof(AddressRequest.AddressTypes),
                nameof(AddressRequest.Street),
                nameof(AddressRequest.Locality),
                nameof(AddressRequest.CountryIso)
            );
    }

    private static IEnumerable<string> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames);
    }
}
