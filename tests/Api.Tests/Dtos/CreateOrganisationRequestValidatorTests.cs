using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data.Postgres;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Dtos.Validation;

namespace Defra.WasteOrganisations.Api.Tests.Dtos;

// The rules are tested through the validator the endpoint uses, so each test also pins the JSON
// path a client sees as the error key
public class CreateOrganisationRequestValidatorTests
{
    private static AddressRequest Address(params AddressType[] kinds) =>
        new()
        {
            AddressTypes = [.. kinds],
            BuildingNumber = "12",
            Street = "High Street",
            Locality = "Testville",
            PostalCode = "AB1 2CD",
            CountryIso = "GB",
        };

    private static OrganisationRequest Ltd() =>
        new()
        {
            Name = "Acme Waste Ltd",
            CompaniesHouseNumber = "12345678",
            RegulatorNation = "GB-ENG",
            OrganisationType = "LTD",
            Addresses = [Address(AddressType.Registered)],
        };

    private static OrganisationRequest SoleTrader() =>
        new()
        {
            TradingName = "Acme",
            RegulatorNation = "GB-WLS",
            OrganisationType = "SOLE_TRADER",
            Addresses = [Address(AddressType.Principal)],
        };

    private static Dictionary<string, string[]> Validate(OrganisationRequest organisation) =>
        CreateOrganisationRequestValidator.Validate(new CreateOrganisationRequest { Organisation = organisation });

    public static TheoryData<string> CompaniesHouseCodes =>
        [.. OrganisationTypeCodes.All.Where(c => c.RegisteredAtCompaniesHouse).Select(c => c.Code)];

    public static TheoryData<string> OtherCodes =>
        [.. OrganisationTypeCodes.All.Where(c => !c.RegisteredAtCompaniesHouse).Select(c => c.Code)];

    [Fact]
    public void Validate_ValidLtd_ShouldHaveNoErrors()
    {
        Validate(Ltd()).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidSoleTrader_ShouldHaveNoErrors()
    {
        Validate(SoleTrader()).Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithoutOrganisation_ShouldReportTheEnvelopeMember()
    {
        CreateOrganisationRequestValidator
            .Validate(new CreateOrganisationRequest())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]> { ["organisation"] = ["The organisation field is required."] }
            );
    }

    [Theory]
    [MemberData(nameof(CompaniesHouseCodes))]
    public void Validate_CompaniesHouseTypeWithoutNameOrNumber_ShouldReportBoth(string code)
    {
        var errors = Validate(
            Ltd() with
            {
                OrganisationType = code,
                Name = null,
                TradingName = "Acme",
                CompaniesHouseNumber = null,
            }
        );

        errors
            .Should()
            .ContainKey("organisation.name")
            .WhoseValue.Should()
            .Equal($"The name field is required for organisation type {code}.");
        errors
            .Should()
            .ContainKey("organisation.companiesHouseNumber")
            .WhoseValue.Should()
            .Equal($"The companiesHouseNumber field is required for organisation type {code}.");
        errors.Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(OtherCodes))]
    public void Validate_OtherTypeWithoutTradingNameAndWithNumber_ShouldReportBoth(string code)
    {
        var errors = Validate(
            SoleTrader() with
            {
                OrganisationType = code,
                Name = "Acme Ltd",
                TradingName = null,
                CompaniesHouseNumber = "12345678",
            }
        );

        errors
            .Should()
            .ContainKey("organisation.tradingName")
            .WhoseValue.Should()
            .Equal($"The tradingName field is required for organisation type {code}.");
        errors
            .Should()
            .ContainKey("organisation.companiesHouseNumber")
            .WhoseValue.Should()
            .Equal($"The companiesHouseNumber field must be absent for organisation type {code}.");
        errors.Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(CompaniesHouseCodes))]
    public void Validate_CompaniesHouseTypeWithPrincipalAddressOnly_ShouldReportAddresses(string code)
    {
        var errors = Validate(Ltd() with { OrganisationType = code, Addresses = [Address(AddressType.Principal)] });

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses"] =
                    [
                        $"The addresses field must hold an address with the REGISTERED type for organisation type {code}.",
                    ],
                }
            );
    }

    [Theory]
    [MemberData(nameof(OtherCodes))]
    public void Validate_OtherTypeWithRegisteredAddressOnly_ShouldReportAddresses(string code)
    {
        var errors = Validate(
            SoleTrader() with
            {
                OrganisationType = code,
                Addresses = [Address(AddressType.Registered)],
            }
        );

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses"] =
                    [
                        $"The addresses field must hold an address with the PRINCIPAL type for organisation type {code}.",
                    ],
                }
            );
    }

    // The type-dependent rules cannot be applied without a known type, so they add nothing
    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("ltd")]
    [InlineData(null)]
    public void Validate_UnknownOrMissingType_ShouldReportTheTypeAlone(string? code)
    {
        var errors = Validate(
            Ltd() with
            {
                OrganisationType = code,
                Name = null,
                CompaniesHouseNumber = null,
                Addresses = [Address(AddressType.Audit)],
            }
        );

        errors.Keys.Should().Equal("organisation.organisationType");
    }

    [Fact]
    public void Validate_BothNamesOnEitherType_ShouldBeAccepted()
    {
        Validate(Ltd() with { TradingName = "Acme" }).Should().BeEmpty();
        Validate(SoleTrader() with { Name = "Acme Ltd" }).Should().BeEmpty();
    }

    // Whitespace is a blank value, not an absent one, so it is never silently dropped
    [Fact]
    public void Validate_BlankNames_ShouldReportEach()
    {
        var errors = Validate(Ltd() with { Name = " ", TradingName = "" });

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.name"] = ["The name field must not be blank."],
                    ["organisation.tradingName"] = ["The tradingName field must not be blank."],
                }
            );
    }

    // Trimmed and upper-cased before the pattern is checked
    [Theory]
    [InlineData(" 12345678 ")]
    [InlineData("SC123456")]
    [InlineData("sc123456")]
    [InlineData("NI012345")]
    [InlineData("OC123456")]
    [InlineData("LP123456")]
    public void Validate_CompaniesHouseNumber_ShouldAcceptTheEightCharacterForm(string number)
    {
        Validate(Ltd() with { CompaniesHouseNumber = number }).Should().BeEmpty();
    }

    // IP12345R is the registered-society form, which no type that may carry a number uses
    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("IP12345R")]
    [InlineData("12 34567")]
    [InlineData("SCA23456")]
    public void Validate_CompaniesHouseNumber_ShouldRejectAnythingElse(string number)
    {
        Validate(Ltd() with { CompaniesHouseNumber = number })
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.companiesHouseNumber"] =
                    [
                        "The companiesHouseNumber field must be two letters or digits followed by six digits, such as 12345678 or SC123456.",
                    ],
                }
            );
    }

    [Fact]
    public void Validate_RegulatorNationMissingOrUnknown_ShouldReportIt()
    {
        Validate(Ltd() with { RegulatorNation = null })
            .Should()
            .ContainKey("organisation.regulatorNation")
            .WhoseValue.Should()
            .Equal("The regulatorNation field is required.");
        Validate(Ltd() with { RegulatorNation = "GB-XXX" })
            .Should()
            .ContainKey("organisation.regulatorNation")
            .WhoseValue.Should()
            .Equal("The regulatorNation field must be one of GB-ENG, GB-SCT, GB-WLS, GB-NIR.");
    }

    [Fact]
    public void Validate_AddressesMissingEmptyOrSix_ShouldReportAddresses()
    {
        Validate(Ltd() with { Addresses = null })["organisation.addresses"]
            .Should()
            .Equal("The addresses field is required.");
        Validate(Ltd() with { Addresses = [] })["organisation.addresses"]
            .Should()
            .Equal("The addresses field must hold at least one item.");
        Validate(Ltd() with { Addresses = [.. Enumerable.Range(0, 6).Select(_ => Address(AddressType.Registered))] })
            .Should()
            .ContainKey("organisation.addresses")
            .WhoseValue.Should()
            .Equal("The addresses field must hold at most five items.");
    }

    [Fact]
    public void Validate_AddressTypesMissingOrEmpty_ShouldReportTheItem()
    {
        var missing = Address() with { AddressTypes = null };
        var empty = Address();

        var errors = Validate(Ltd() with { Addresses = [Address(AddressType.Registered), missing, empty] });

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses[1].addressTypes"] = ["The addressTypes field is required."],
                    ["organisation.addresses[2].addressTypes"] =
                    [
                        "The addressTypes field must hold at least one item.",
                    ],
                }
            );
    }

    [Fact]
    public void Validate_KindRepeatedInOneItem_ShouldReportTheItem()
    {
        var errors = Validate(
            Ltd() with
            {
                Addresses = [Address(AddressType.Registered, AddressType.Audit, AddressType.Registered)],
            }
        );

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses[0].addressTypes"] =
                    [
                        "The addressTypes field must not repeat a value: REGISTERED.",
                    ],
                }
            );
    }

    // Reported under the later item, naming the item that already carries the kind
    [Fact]
    public void Validate_KindRepeatedAcrossItems_ShouldReportTheLaterItem()
    {
        var errors = Validate(
            Ltd() with
            {
                Addresses =
                [
                    Address(AddressType.Registered),
                    Address(AddressType.Audit),
                    Address(AddressType.Correspondence, AddressType.Registered),
                ],
            }
        );

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses[2].addressTypes"] =
                    [
                        "The addressTypes field must not repeat REGISTERED, which addresses[0] already carries.",
                    ],
                }
            );
    }

    // One item may carry every kind
    [Fact]
    public void Validate_OneItemWithAllFiveKinds_ShouldBeAccepted()
    {
        Validate(Ltd() with { Addresses = [Address(Enum.GetValues<AddressType>())] }).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_UkAddressWithoutPostcode_ShouldReportPostalCode(string? postalCode)
    {
        var errors = Validate(
            Ltd() with
            {
                Addresses = [Address(AddressType.Registered) with { PostalCode = postalCode }],
            }
        );

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses[0].postalCode"] =
                    [
                        "The postalCode field is required when countryIso is GB.",
                    ],
                }
            );
    }

    // Some countries have no postcodes, and a blank optional part is stored as null
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Validate_NonUkAddressWithoutPostcode_ShouldBeAccepted(string? postalCode)
    {
        var overseas = Address(AddressType.Correspondence) with { PostalCode = postalCode, CountryIso = "HK" };

        Validate(Ltd() with { Addresses = [Address(AddressType.Registered), overseas] }).Should().BeEmpty();
    }

    // A lower-case or padded GB is still GB, so the postcode rule applies to it
    [Fact]
    public void Validate_PaddedLowerCaseCountryIso_ShouldBeAcceptedAndTreatedAsGb()
    {
        Validate(Ltd() with { Addresses = [Address(AddressType.Registered) with { CountryIso = " gb " }] })
            .Should()
            .BeEmpty();
        Validate(
            Ltd() with
            {
                Addresses = [Address(AddressType.Registered) with { CountryIso = " gb ", PostalCode = null }],
            }
        )
            .Keys.Should()
            .Equal("organisation.addresses[0].postalCode");
    }

    [Theory]
    [InlineData("GBR")]
    [InlineData("G")]
    [InlineData("G1")]
    public void Validate_CountryIsoNotTwoLetters_ShouldReportCountryIso(string countryIso)
    {
        var errors = Validate(
            Ltd() with
            {
                Addresses = [Address(AddressType.Registered) with { CountryIso = countryIso }],
            }
        );

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses[0].countryIso"] =
                    [
                        "The countryIso field must be an ISO 3166-1 alpha-2 code of two letters, such as GB.",
                    ],
                }
            );
    }

    // Mirrors ck_address_premises; no single field is at fault, so the item itself is the key
    [Fact]
    public void Validate_AddressWithoutPremises_ShouldReportTheItem()
    {
        var noPremises = Address(AddressType.Registered) with { BuildingNumber = " ", BuildingName = "" };

        Validate(Ltd() with { Addresses = [noPremises] })
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses[0]"] =
                    [
                        "At least one of subBuildingName, buildingName, buildingNumber and poBox is required.",
                    ],
                }
            );
    }

    [Theory]
    [InlineData("subBuildingName")]
    [InlineData("buildingName")]
    [InlineData("poBox")]
    public void Validate_AnyOnePremisesPart_ShouldBeEnough(string part)
    {
        var address = Address(AddressType.Registered) with
        {
            BuildingNumber = null,
            SubBuildingName = part == "subBuildingName" ? "Unit 4" : null,
            BuildingName = part == "buildingName" ? "Mill House" : null,
            PoBox = part == "poBox" ? "PO Box 99" : null,
        };

        Validate(Ltd() with { Addresses = [address] }).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Validate_StreetLocalityAndCountryMissingOrBlank_ShouldReportEach(string? value)
    {
        var errors = Validate(
            Ltd() with
            {
                Addresses =
                [
                    Address(AddressType.Registered) with
                    {
                        Street = value,
                        Locality = value,
                        CountryIso = value,
                    },
                ],
            }
        );

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["organisation.addresses[0].street"] = ["The street field is required."],
                    ["organisation.addresses[0].locality"] = ["The locality field is required."],
                    ["organisation.addresses[0].countryIso"] = ["The countryIso field is required."],
                }
            );
    }

    // The framework validator would skip the organisation's rules here, because an attribute fails
    // below it; every failing field must be reported (Jira Scenario 2)
    [Fact]
    public void Validate_AttributeErrorInAnItemAndRuleErrorOnTheOrganisation_ShouldReportBoth()
    {
        var errors = Validate(
            SoleTrader() with
            {
                TradingName = null,
                Addresses = [Address(AddressType.Principal) with { Street = null }],
            }
        );

        errors.Keys.Should().BeEquivalentTo("organisation.addresses[0].street", "organisation.tradingName");
    }

    [Fact]
    public void Validate_NullAddressItem_ShouldReportTheItem()
    {
        var errors = Validate(Ltd() with { Addresses = [Address(AddressType.Registered), null!] });

        errors
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]> { ["organisation.addresses[1]"] = ["The address must not be null."] }
            );
    }
}
