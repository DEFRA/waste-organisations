using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The v2 create against the Compose stack, checked through the response and the stored rows.
// Address rows are shared and never cleaned up, so every test sends address values unique to it
// (a suffix in `addressLine1`) and counts addresses through its own organisation's links, never the
// whole address table
public class CreateOrganisationTests : IntegrationTestBase
{
    public static TheoryData<string, string?> OrganisationTypes =>
        new(OrganisationTypeCodes.All.Select(t => (t.Code, NumberFor(t.Code))));

    private static readonly string[] Registered = ["REGISTERED"];

    // The full contract in one request: a Companies House type, one UK address carrying two kinds
    // and a non-UK address without a postcode. Pins the 201 body and every stored row
    [Fact]
    public async Task FullPayload_ShouldCreateOrganisationAndRows()
    {
        var suffix = NewSuffix();
        var body = new
        {
            organisation = new
            {
                name = "Acme Waste Ltd",
                tradingName = "Acme Recycling",
                companiesHouseNumber = CompaniesHouseNumbers.FullPayload,
                regulatorNation = "GB-ENG",
                organisationType = "LTD",
                addresses = new object[]
                {
                    new
                    {
                        addressTypes = new[] { "REGISTERED", "PRINCIPAL" },
                        addressLine1 = $"12A High Street {suffix}",
                        addressLine2 = "Mill House",
                        townOrCity = "Testville",
                        county = "Testshire",
                        postcode = "AB1 2CD",
                        countryIso = "GB",
                    },
                    new
                    {
                        addressTypes = new[] { "CORRESPONDENCE" },
                        addressLine1 = $"12 Harbour Road {suffix}",
                        townOrCity = "Hong Kong",
                        countryIso = "HK",
                    },
                },
            },
        };

        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        var created = await ReadOrganisationAsync(response);
        response.Headers.Location.Should().Be($"/organisations/{created.Id}");
        created.CreatedAt.Should().Be(created.ValidFrom);
        created.UpdatedAt.Should().Be(created.ValidFrom);

        var stored = await ReadStoredAsync(created.Id);
        stored.Name.Should().Be("Acme Waste Ltd");
        stored.TradingName.Should().Be("Acme Recycling");
        stored.CompaniesHouseNumber.Should().Be(CompaniesHouseNumbers.FullPayload);
        stored.RegulatorNation.Should().Be("GB-ENG");
        stored.OrganisationType!.Code.Should().Be("LTD");
        stored.ValidFrom.Should().Be(created.ValidFrom.UtcDateTime);
        stored.ValidTo.Should().BeNull();
        stored.CreatedAt.Should().Be(created.CreatedAt.UtcDateTime);
        stored.UpdatedAt.Should().Be(created.UpdatedAt.UtcDateTime);

        stored.Addresses.Should().HaveCount(3);
        stored
            .Addresses.Should()
            .AllSatisfy(link =>
            {
                link.ValidFrom.Should().Be(stored.ValidFrom);
                link.ValidTo.Should().BeNull();
            });
        var registered = LinkOf(stored, "REGISTERED");
        LinkOf(stored, "PRINCIPAL").AddressId.Should().Be(registered.AddressId);
        var correspondence = LinkOf(stored, "CORRESPONDENCE");
        correspondence.AddressId.Should().NotBe(registered.AddressId);

        registered
            .Address.Should()
            .BeEquivalentTo(
                new Address
                {
                    Id = registered.AddressId,
                    AddressLine1 = $"12A High Street {suffix}",
                    AddressLine2 = "Mill House",
                    TownOrCity = "Testville",
                    County = "Testshire",
                    Postcode = "AB1 2CD",
                    CountryIso = "GB",
                },
                options => options.Excluding(a => a.CreatedAt).Excluding(a => a.UpdatedAt)
            );
        correspondence
            .Address.Should()
            .BeEquivalentTo(
                new Address
                {
                    Id = correspondence.AddressId,
                    AddressLine1 = $"12 Harbour Road {suffix}",
                    TownOrCity = "Hong Kong",
                    CountryIso = "HK",
                },
                options => options.Excluding(a => a.CreatedAt).Excluding(a => a.UpdatedAt)
            );

        // The suffix is this run's alone, so it is taken out and the snapshot reads as a plain address
        await VerifyJson(content).AddScrubber(text => text.Replace($" {suffix}", ""));
    }

    // One item may carry every kind: the kinds come back in the order sent, and the address is
    // stored once with a link per kind
    [Fact]
    public async Task OneAddressWithAllFiveKinds_ShouldKeepTheOrderAndStoreOneAddress()
    {
        string[] kinds = ["CORRESPONDENCE", "AUDIT", "REGISTERED", "SERVICE_OF_NOTICE", "PRINCIPAL"];
        var body = Ltd(CompaniesHouseNumbers.CreateFiveKinds, Address(NewSuffix(), kinds));

        var response = await PostAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadOrganisationAsync(response);
        created.Addresses.Should().ContainSingle();
        created.Addresses[0].AddressTypes.Select(t => t.ToJsonValue()).Should().Equal(kinds);

        var stored = await ReadStoredAsync(created.Id);
        stored.Addresses.Select(a => a.UsageType.Name).Should().BeEquivalentTo(kinds);
        stored.Addresses.Select(a => a.AddressId).Distinct().Should().ContainSingle();
    }

    // Five items with different values are five address rows, one link each
    [Fact]
    public async Task FiveDifferentAddresses_ShouldStoreFiveAddresses()
    {
        string[] kinds = ["REGISTERED", "PRINCIPAL", "AUDIT", "SERVICE_OF_NOTICE", "CORRESPONDENCE"];
        var body = Ltd(
            CompaniesHouseNumbers.CreateFiveAddresses,
            kinds.Select(kind => Address(NewSuffix(), kind)).ToArray()
        );

        var response = await PostAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadOrganisationAsync(response);
        created.Addresses.Should().HaveCount(5);

        var stored = await ReadStoredAsync(created.Id);
        stored.Addresses.Should().HaveCount(5);
        stored.Addresses.Select(a => a.AddressId).Distinct().Should().HaveCount(5);
    }

    // A non-Companies House type is identified by its trading name: `name` may be absent, is
    // stored as null and is omitted from the response
    [Fact]
    public async Task SoleTraderWithoutName_ShouldStoreNullName()
    {
        var body = new
        {
            organisation = new
            {
                tradingName = "Sam's Skips",
                regulatorNation = "GB-WLS",
                organisationType = "SOLE_TRADER",
                addresses = new[] { Address(NewSuffix(), "PRINCIPAL") },
            },
        };

        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        content.Should().NotContain("\"name\"");
        var created = await ReadOrganisationAsync(response);
        created.Name.Should().BeNull();
        created.TradingName.Should().Be("Sam's Skips");
        created.CompaniesHouseNumber.Should().BeNull();

        var stored = await ReadStoredAsync(created.Id);
        stored.Name.Should().BeNull();
        stored.TradingName.Should().Be("Sam's Skips");
        stored.CompaniesHouseNumber.Should().BeNull();
    }

    // Either type may send both names; both are stored and returned
    [Theory]
    [InlineData("LTD", CompaniesHouseNumbers.CreateNameAndTradingName)]
    [InlineData("SOLE_TRADER", null)]
    public async Task NameAndTradingName_ShouldBothBeStored(string organisationType, string? companiesHouseNumber)
    {
        var registered = IsRegisteredAtCompaniesHouse(organisationType);
        var body = new
        {
            organisation = new
            {
                name = "Acme Holdings",
                tradingName = "Acme Skips",
                companiesHouseNumber,
                regulatorNation = "GB-SCT",
                organisationType,
                addresses = new[] { Address(NewSuffix(), RequiredKind(registered)) },
            },
        };

        var response = await PostAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadOrganisationAsync(response);
        created.Name.Should().Be("Acme Holdings");
        created.TradingName.Should().Be("Acme Skips");

        var stored = await ReadStoredAsync(created.Id);
        stored.Name.Should().Be("Acme Holdings");
        stored.TradingName.Should().Be("Acme Skips");
    }

    // Strings are trimmed, the Companies House number, country code and postcode upper-cased, and
    // a GB postcode spaced as outward code, one space, inward code, before they are stored and
    // returned; other parts keep their case
    [Fact]
    public async Task Values_ShouldBeTrimmedAndUpperCased()
    {
        var suffix = NewSuffix();
        var body = new
        {
            organisation = new
            {
                name = "  Acme Waste Ltd  ",
                tradingName = " Acme Recycling ",
                companiesHouseNumber = $" {CompaniesHouseNumbers.CreateTrimmedAndUpperCased.ToLowerInvariant()} ",
                regulatorNation = " GB-NIR ",
                organisationType = " LTD ",
                addresses = new[]
                {
                    new
                    {
                        addressTypes = new[] { "REGISTERED" },
                        addressLine1 = $"  12 High Street {suffix} ",
                        townOrCity = " Testville ",
                        county = "   ",
                        postcode = " ab1-2cd ",
                        countryIso = " gb ",
                    },
                },
            },
        };

        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        var created = await ReadOrganisationAsync(response);
        created.Name.Should().Be("Acme Waste Ltd");
        created.TradingName.Should().Be("Acme Recycling");
        created.CompaniesHouseNumber.Should().Be(CompaniesHouseNumbers.CreateTrimmedAndUpperCased);
        created.RegulatorNation.Should().Be(RegulatorNation.NorthernIreland);
        created.OrganisationType.Should().Be("LTD");
        created.Addresses[0].AddressLine1.Should().Be($"12 High Street {suffix}");
        created.Addresses[0].Postcode.Should().Be("AB1 2CD");
        created.Addresses[0].CountryIso.Should().Be("GB");

        var stored = await ReadStoredAsync(created.Id);
        stored.Name.Should().Be("Acme Waste Ltd");
        stored.TradingName.Should().Be("Acme Recycling");
        stored.CompaniesHouseNumber.Should().Be(CompaniesHouseNumbers.CreateTrimmedAndUpperCased);
        stored.RegulatorNation.Should().Be("GB-NIR");
        var address = stored.Addresses.Single().Address;
        address.AddressLine1.Should().Be($"12 High Street {suffix}");
        address.TownOrCity.Should().Be("Testville");
        address.County.Should().BeNull();
        address.Postcode.Should().Be("AB1 2CD");
        address.CountryIso.Should().Be("GB");
    }

    // Server-set and unknown members, the old address parts among them, are ignored rather than
    // rejected, so a client ahead of the server is not broken; the server's own values come back
    [Fact]
    public async Task ServerSetAndUnknownMembers_ShouldBeIgnored()
    {
        var sentId = Guid.NewGuid();
        var sentDate = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var suffix = NewSuffix();
        var body = new
        {
            organisation = new
            {
                id = sentId,
                name = "Acme Waste Ltd",
                companiesHouseNumber = CompaniesHouseNumbers.CreateServerSetMembers,
                regulatorNation = "GB-ENG",
                organisationType = "LTD",
                mainActivitySicCode = "38110",
                validFrom = sentDate,
                createdAt = sentDate,
                updatedAt = sentDate,
                addresses = new[]
                {
                    new
                    {
                        addressTypes = new[] { "REGISTERED" },
                        addressLine = "Somewhere else entirely",
                        street = "Old Street",
                        addressLine1 = $"12 High Street {suffix}",
                        townOrCity = "Testville",
                        postcode = "AB1 2CD",
                        postalCode = "ZZ9 9ZZ",
                        countryIso = "GB",
                        countryName = "United Kingdom",
                    },
                },
            },
        };
        var before = DateTimeOffset.UtcNow.AddMinutes(-5);

        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        content.Should().NotContain("mainActivitySicCode").And.NotContain("countryName");
        var created = await ReadOrganisationAsync(response);
        created.Id.Should().NotBe(sentId);
        created.ValidFrom.Should().BeAfter(before);
        created.CreatedAt.Should().BeAfter(before);
        created.UpdatedAt.Should().BeAfter(before);
        content.Should().NotContain("\"addressLine\"").And.NotContain("\"street\"").And.NotContain("\"postalCode\"");
        created.Addresses[0].AddressLine1.Should().Be($"12 High Street {suffix}");
        created.Addresses[0].Postcode.Should().Be("AB1 2CD");

        var stored = await ReadStoredAsync(created.Id);
        stored.ValidFrom.Should().Be(created.ValidFrom.UtcDateTime);
        var address = stored.Addresses.Single().Address;
        address.Postcode.Should().Be("AB1 2CD");
    }

    // A GB postcode with punctuation is stored and returned as outward code, one space, inward code
    [Fact]
    public async Task PostcodeWithPunctuation_ShouldBeStoredAndReturnedNormalised()
    {
        var suffix = NewSuffix();
        var body = Ltd(
            CompaniesHouseNumbers.CreatePunctuatedPostcode,
            new
            {
                addressTypes = Registered,
                addressLine1 = $"12 High Street {suffix}",
                townOrCity = "Testville",
                postcode = "ab1-2cd",
                countryIso = "GB",
            }
        );

        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        var created = await ReadOrganisationAsync(response);
        created.Addresses[0].Postcode.Should().Be("AB1 2CD");

        var stored = await ReadStoredAsync(created.Id);
        stored.Addresses.Single().Address.Postcode.Should().Be("AB1 2CD");
    }

    // A non-UK postcode is not checked against the UK format and keeps its punctuation
    [Fact]
    public async Task NonUkAddressWithPunctuatedPostcode_ShouldKeepIt()
    {
        var suffix = NewSuffix();
        var body = Ltd(
            CompaniesHouseNumbers.CreateNonUkPostcode,
            new
            {
                addressTypes = Registered,
                addressLine1 = $"1600 Main Street {suffix}",
                townOrCity = "Springfield",
                county = "Illinois",
                postcode = "12345-6789",
                countryIso = "US",
            }
        );

        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        var created = await ReadOrganisationAsync(response);
        created.Addresses[0].Postcode.Should().Be("12345-6789");

        var stored = await ReadStoredAsync(created.Id);
        var address = stored.Addresses.Single().Address;
        address.Postcode.Should().Be("12345-6789");
        address.CountryIso.Should().Be("US");
    }

    // Every code is accepted with what its RegisteredAtCompaniesHouse flag requires: a number and a
    // REGISTERED address, or a trading name and a PRINCIPAL address. Pins the flag of every code
    // against the seeded organisation_type rows
    [Theory]
    [MemberData(nameof(OrganisationTypes))]
    public async Task EachOrganisationType_WithWhatItsFlagRequires_ShouldCreate(
        string organisationType,
        string? companiesHouseNumber
    )
    {
        var registered = IsRegisteredAtCompaniesHouse(organisationType);
        var body = new
        {
            organisation = new
            {
                name = registered ? "Acme Waste" : null,
                tradingName = registered ? null : "Acme Waste",
                companiesHouseNumber,
                regulatorNation = "GB-ENG",
                organisationType,
                addresses = new[] { Address(NewSuffix(), RequiredKind(registered)) },
            },
        };

        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        var created = await ReadOrganisationAsync(response);
        created.OrganisationType.Should().Be(organisationType);

        var stored = await ReadStoredAsync(created.Id);
        stored.OrganisationType!.Code.Should().Be(organisationType);
    }

    // Two items holding the same address, postcode spelling aside, are the same address: stored
    // once, linked once per kind, and returned as one item whose kinds are those of both items in
    // the order sent
    [Fact]
    public async Task SameAddressInTwoItems_ShouldStoreOneAddressAndReturnOneItem()
    {
        var suffix = NewSuffix();
        var unspacedPostcode = new
        {
            addressTypes = new[] { "REGISTERED", "AUDIT" },
            addressLine1 = $"12 High Street {suffix}",
            townOrCity = "Testville",
            postcode = "ab12cd",
            countryIso = "GB",
        };
        var body = Ltd(CompaniesHouseNumbers.CreateSameAddressTwoItems, Address(suffix, "PRINCIPAL"), unspacedPostcode);

        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        var created = await ReadOrganisationAsync(response);
        created.Addresses.Should().ContainSingle();
        created
            .Addresses[0]
            .AddressTypes.Select(t => t.ToJsonValue())
            .Should()
            .Equal("PRINCIPAL", "REGISTERED", "AUDIT");

        var stored = await ReadStoredAsync(created.Id);
        stored.Addresses.Should().HaveCount(3);
        stored.Addresses.Select(a => a.AddressId).Distinct().Should().ContainSingle();
        (await CountAddressesAsync(suffix)).Should().Be(1);
    }

    // An address another organisation already stored is linked, not inserted again. A 500 here
    // means the service left a link on its candidate, so the save hit ux_address_values twice
    [Fact]
    public async Task AddressStoredByAnotherOrganisation_ShouldBeLinkedNotInserted()
    {
        var suffix = NewSuffix();

        var first = await PostAsync(Ltd(CompaniesHouseNumbers.CreateSharedAddressFirst, Address(suffix, "REGISTERED")));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstCreated = await ReadOrganisationAsync(first);

        var second = await PostAsync(
            Ltd(CompaniesHouseNumbers.CreateSharedAddressSecond, Address(suffix, "REGISTERED", "AUDIT"))
        );
        var content = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.Created, content);
        var secondCreated = await ReadOrganisationAsync(second);
        secondCreated.Addresses.Should().ContainSingle();
        secondCreated.Addresses[0].AddressTypes.Select(t => t.ToJsonValue()).Should().Equal("REGISTERED", "AUDIT");

        var firstStored = await ReadStoredAsync(firstCreated.Id);
        var secondStored = await ReadStoredAsync(secondCreated.Id);
        var addressId = firstStored.Addresses.Single().AddressId;
        secondStored.Addresses.Select(a => a.AddressId).Should().AllBeEquivalentTo(addressId);
        (await CountAddressesAsync(suffix)).Should().Be(1);
        (await CountLinksAsync(addressId)).Should().Be(3);
    }

    // An absent optional part and a blank one are both stored as null, so they are one address;
    // the second request finds the first's row through the IS NULL match on addressLine2 and county
    [Fact]
    public async Task AddressLine2AndCountyAbsentAndBlank_ShouldBeOneAddress()
    {
        var suffix = NewSuffix();
        var absent = new
        {
            addressTypes = new[] { "REGISTERED" },
            addressLine1 = $"12 High Street {suffix}",
            townOrCity = "Testville",
            postcode = "AB1 2CD",
            countryIso = "GB",
        };
        var blank = new
        {
            addressTypes = new[] { "REGISTERED" },
            addressLine1 = $"12 High Street {suffix}",
            addressLine2 = " ",
            townOrCity = "Testville",
            county = "  ",
            postcode = "AB1 2CD",
            countryIso = "GB",
        };

        var first = await PostAsync(Ltd(CompaniesHouseNumbers.CreateAbsentParts, absent));
        var second = await PostAsync(Ltd(CompaniesHouseNumbers.CreateBlankParts, blank));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstStored = await ReadStoredAsync((await ReadOrganisationAsync(first)).Id);
        var secondStored = await ReadStoredAsync((await ReadOrganisationAsync(second)).Id);
        secondStored.Addresses.Single().AddressId.Should().Be(firstStored.Addresses.Single().AddressId);
        secondStored.Addresses.Single().Address.AddressLine2.Should().BeNull();
        secondStored.Addresses.Single().Address.County.Should().BeNull();
        (await CountAddressesAsync(suffix)).Should().Be(1);
    }

    // The match is exact over the six parts: one part more, one part different or a difference
    // in case alone makes another address, stored as a second row
    [Theory]
    [InlineData(
        "addressLine1 in upper case",
        CompaniesHouseNumbers.CreateVariantUpperCaseFirst,
        CompaniesHouseNumbers.CreateVariantUpperCaseSecond
    )]
    [InlineData(
        "other postcode",
        CompaniesHouseNumbers.CreateVariantPostcodeFirst,
        CompaniesHouseNumbers.CreateVariantPostcodeSecond
    )]
    [InlineData(
        "addressLine2 added",
        CompaniesHouseNumbers.CreateVariantAddressLine2First,
        CompaniesHouseNumbers.CreateVariantAddressLine2Second
    )]
    [InlineData(
        "county added",
        CompaniesHouseNumbers.CreateVariantCountyFirst,
        CompaniesHouseNumbers.CreateVariantCountySecond
    )]
    [InlineData(
        "other townOrCity",
        CompaniesHouseNumbers.CreateVariantTownFirst,
        CompaniesHouseNumbers.CreateVariantTownSecond
    )]
    [InlineData(
        "other countryIso",
        CompaniesHouseNumbers.CreateVariantCountryFirst,
        CompaniesHouseNumbers.CreateVariantCountrySecond
    )]
    public async Task AddressDifferingInOnePart_ShouldBeStoredAsSecondRow(
        string difference,
        string firstNumber,
        string secondNumber
    )
    {
        var suffix = NewSuffix();
        var variant = difference switch
        {
            "addressLine1 in upper case" => VariantAddress(addressLine1: $"12 HIGH STREET {suffix}"),
            "other postcode" => VariantAddress(addressLine1: $"12 High Street {suffix}", postcode: "AB1 2CE"),
            "addressLine2 added" => VariantAddress(
                addressLine1: $"12 High Street {suffix}",
                addressLine2: "Mill House"
            ),
            "county added" => VariantAddress(addressLine1: $"12 High Street {suffix}", county: "Testshire"),
            "other townOrCity" => VariantAddress(addressLine1: $"12 High Street {suffix}", townOrCity: "Otherville"),
            "other countryIso" => VariantAddress(
                addressLine1: $"12 High Street {suffix}",
                postcode: "AB1 2CD",
                countryIso: "IE"
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(difference), difference, null),
        };

        var first = await PostAsync(Ltd(firstNumber, Address(suffix, "REGISTERED")));
        var second = await PostAsync(Ltd(secondNumber, variant));
        var content = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created, content);
        var firstStored = await ReadStoredAsync((await ReadOrganisationAsync(first)).Id);
        var secondStored = await ReadStoredAsync((await ReadOrganisationAsync(second)).Id);
        secondStored.Addresses.Single().AddressId.Should().NotBe(firstStored.Addresses.Single().AddressId);
        (await CountAddressesAsync(suffix)).Should().Be(2);
    }

    private static bool IsRegisteredAtCompaniesHouse(string code) =>
        OrganisationTypeCodes.All.Single(t => t.Code == code).RegisteredAtCompaniesHouse;

    private static string? NumberFor(string organisationType) =>
        organisationType switch
        {
            "LTD" => CompaniesHouseNumbers.CreateTypeLtd,
            "PLC" => CompaniesHouseNumbers.CreateTypePlc,
            "LIMITED_PARTNERSHIP" => CompaniesHouseNumbers.CreateTypeLimitedPartnership,
            "LLP" => CompaniesHouseNumbers.CreateTypeLlp,
            "CIC" => CompaniesHouseNumbers.CreateTypeCic,
            _ => null,
        };

    private static string RequiredKind(bool registeredAtCompaniesHouse) =>
        registeredAtCompaniesHouse ? "REGISTERED" : "PRINCIPAL";

    private static object Address(string suffix, params string[] addressTypes) =>
        new
        {
            addressTypes,
            addressLine1 = $"12 High Street {suffix}",
            townOrCity = "Testville",
            postcode = "AB1 2CD",
            countryIso = "GB",
        };

    // Address(suffix, "REGISTERED") with one part changed, added or removed
    private static object VariantAddress(
        string addressLine1,
        string? addressLine2 = null,
        string? county = null,
        string postcode = "AB1 2CD",
        string townOrCity = "Testville",
        string countryIso = "GB"
    ) =>
        new
        {
            addressTypes = new[] { "REGISTERED" },
            addressLine1,
            addressLine2,
            townOrCity,
            county,
            postcode,
            countryIso,
        };

    private static object Ltd(string companiesHouseNumber, params object[] addresses) =>
        new
        {
            organisation = new
            {
                name = "Acme Waste Ltd",
                companiesHouseNumber,
                regulatorNation = "GB-ENG",
                organisationType = "LTD",
                addresses,
            },
        };

    private static OrganisationAddress LinkOf(Organisation organisation, string usageType) =>
        organisation.Addresses.Single(a => a.UsageType.Name == usageType);

    private static async Task<HttpResponseMessage> PostAsync(object body)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Testing.Endpoints.Organisations.Post())
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("Accept", AcceptV2);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<OrganisationResponse> ReadOrganisationAsync(HttpResponseMessage response) =>
        (
            await response.Content.ReadFromJsonAsync<CreateOrganisationResponse>(TestContext.Current.CancellationToken)
        )!.Organisation;

    // The address rows holding this test's suffix, which no other test sends
    private static async Task<int> CountAddressesAsync(string suffix)
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        return await context.Addresses.CountAsync(
            a => a.AddressLine1 != null && a.AddressLine1.Contains(suffix),
            TestContext.Current.CancellationToken
        );
    }

    // The organisation_address rows of every organisation that point at the address
    private static async Task<int> CountLinksAsync(Guid addressId)
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        return await context.OrganisationAddresses.CountAsync(
            a => a.AddressId == addressId,
            TestContext.Current.CancellationToken
        );
    }

    private static async Task<Organisation> ReadStoredAsync(Guid id)
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        return await context
            .Organisations.AsNoTracking()
            .Include(o => o.OrganisationType)
            .Include(o => o.Addresses)
                .ThenInclude(a => a.Address)
            .Include(o => o.Addresses)
                .ThenInclude(a => a.UsageType)
            .SingleAsync(o => o.Id == id, TestContext.Current.CancellationToken);
    }
}
