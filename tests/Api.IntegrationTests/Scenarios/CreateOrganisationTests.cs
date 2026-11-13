using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The v2 create against the Compose stack, checked through the response and the stored rows.
// Address rows are shared and never cleaned up, so every test sends address values unique to it
// (a suffix in `street`) and counts addresses through its own organisation's links, never the
// whole address table
public class CreateOrganisationTests : IntegrationTestBase
{
    public static TheoryData<string> OrganisationTypes => new(OrganisationTypeCodes.All.Select(t => t.Code));

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
                companiesHouseNumber = "12345678",
                regulatorNation = "GB-ENG",
                organisationType = "LTD",
                addresses = new object[]
                {
                    new
                    {
                        addressTypes = new[] { "REGISTERED", "PRINCIPAL" },
                        subBuildingName = "Unit 4",
                        buildingName = "Mill House",
                        buildingNumber = "12A",
                        street = $"High Street {suffix}",
                        locality = "Testville",
                        region = "Testshire",
                        postalCode = "AB1 2CD",
                        poBox = "PO Box 99",
                        countryIso = "GB",
                    },
                    new
                    {
                        addressTypes = new[] { "CORRESPONDENCE" },
                        buildingNumber = "12",
                        street = $"Harbour Road {suffix}",
                        locality = "Hong Kong",
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
        stored.CompaniesHouseNumber.Should().Be("12345678");
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

        var uk = registered.Address;
        uk.SubBuildingName.Should().Be("Unit 4");
        uk.BuildingName.Should().Be("Mill House");
        uk.BuildingNumber.Should().Be("12A");
        uk.Street.Should().Be($"High Street {suffix}");
        uk.Locality.Should().Be("Testville");
        uk.Region.Should().Be("Testshire");
        uk.PostalCode.Should().Be("AB1 2CD");
        uk.PoBox.Should().Be("PO Box 99");
        uk.CountryIso.Should().Be("GB");

        var overseas = correspondence.Address;
        overseas.SubBuildingName.Should().BeNull();
        overseas.BuildingName.Should().BeNull();
        overseas.BuildingNumber.Should().Be("12");
        overseas.Street.Should().Be($"Harbour Road {suffix}");
        overseas.Locality.Should().Be("Hong Kong");
        overseas.Region.Should().BeNull();
        overseas.PostalCode.Should().BeNull();
        overseas.PoBox.Should().BeNull();
        overseas.CountryIso.Should().Be("HK");

        // The suffix is this run's alone, so it is taken out and the snapshot reads as a plain address
        await VerifyJson(content).AddScrubber(text => text.Replace($" {suffix}", ""));
    }

    // One item may carry every kind: the kinds come back in the order sent, and the address is
    // stored once with a link per kind
    [Fact]
    public async Task OneAddressWithAllFiveKinds_ShouldKeepTheOrderAndStoreOneAddress()
    {
        string[] kinds = ["CORRESPONDENCE", "AUDIT", "REGISTERED", "SERVICE_OF_NOTICE", "PRINCIPAL"];
        var body = Ltd(Address(NewSuffix(), kinds));

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
        var body = Ltd(kinds.Select(kind => Address(NewSuffix(), kind)).ToArray());

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
    [InlineData("LTD")]
    [InlineData("SOLE_TRADER")]
    public async Task NameAndTradingName_ShouldBothBeStored(string organisationType)
    {
        var registered = IsRegisteredAtCompaniesHouse(organisationType);
        var body = new
        {
            organisation = new
            {
                name = "Acme Holdings",
                tradingName = "Acme Skips",
                companiesHouseNumber = registered ? "SC123456" : null,
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
    // the postcode's inner whitespace collapsed, before they are stored and returned; other parts
    // keep their case
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
                companiesHouseNumber = " sc123456 ",
                regulatorNation = " GB-NIR ",
                organisationType = " LTD ",
                addresses = new[]
                {
                    new
                    {
                        addressTypes = new[] { "REGISTERED" },
                        buildingNumber = " 12 ",
                        street = $"  High Street {suffix} ",
                        locality = " Testville ",
                        region = "   ",
                        postalCode = " ab1   2cd ",
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
        created.CompaniesHouseNumber.Should().Be("SC123456");
        created.RegulatorNation.Should().Be(RegulatorNation.NorthernIreland);
        created.OrganisationType.Should().Be("LTD");
        created.Addresses[0].Street.Should().Be($"High Street {suffix}");
        created.Addresses[0].PostalCode.Should().Be("AB1 2CD");
        created.Addresses[0].CountryIso.Should().Be("GB");

        var stored = await ReadStoredAsync(created.Id);
        stored.Name.Should().Be("Acme Waste Ltd");
        stored.TradingName.Should().Be("Acme Recycling");
        stored.CompaniesHouseNumber.Should().Be("SC123456");
        stored.RegulatorNation.Should().Be("GB-NIR");
        var address = stored.Addresses.Single().Address;
        address.BuildingNumber.Should().Be("12");
        address.Street.Should().Be($"High Street {suffix}");
        address.Locality.Should().Be("Testville");
        address.Region.Should().BeNull();
        address.PostalCode.Should().Be("AB1 2CD");
        address.CountryIso.Should().Be("GB");
    }

    // Server-set and unknown members are ignored rather than rejected, so a client ahead of the
    // server is not broken; the server's own values come back
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
                companiesHouseNumber = "12345678",
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
                        buildingNumber = "12",
                        street = $"High Street {suffix}",
                        locality = "Testville",
                        postalCode = "AB1 2CD",
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
        created.Addresses[0].AddressLine.Should().Be($"12 High Street {suffix}, Testville, AB1 2CD");

        var stored = await ReadStoredAsync(created.Id);
        stored.ValidFrom.Should().Be(created.ValidFrom.UtcDateTime);
    }

    // Every code is accepted with what its RegisteredAtCompaniesHouse flag requires: a number and a
    // REGISTERED address, or a trading name and a PRINCIPAL address. Pins the flag of every code
    // against the seeded organisation_type rows
    [Theory]
    [MemberData(nameof(OrganisationTypes))]
    public async Task EachOrganisationType_WithWhatItsFlagRequires_ShouldCreate(string organisationType)
    {
        var registered = IsRegisteredAtCompaniesHouse(organisationType);
        var body = new
        {
            organisation = new
            {
                name = registered ? "Acme Waste" : null,
                tradingName = registered ? null : "Acme Waste",
                companiesHouseNumber = registered ? "OC123456" : null,
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

    // Two items holding the same address are the same address: stored once, linked once per kind,
    // and returned as one item whose kinds are those of both items in the order sent
    [Fact]
    public async Task SameAddressInTwoItems_ShouldStoreOneAddressAndReturnOneItem()
    {
        var suffix = NewSuffix();
        var body = Ltd(Address(suffix, "PRINCIPAL"), Address(suffix, "REGISTERED", "AUDIT"));

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

        var first = await PostAsync(Ltd(Address(suffix, "REGISTERED")));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstCreated = await ReadOrganisationAsync(first);

        var second = await PostAsync(Ltd(Address(suffix, "REGISTERED", "AUDIT")));
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
    // the second request finds the first's row through the IS NULL match on region
    [Fact]
    public async Task RegionAbsentAndBlank_ShouldBeOneAddress()
    {
        var suffix = NewSuffix();
        var absent = new
        {
            addressTypes = new[] { "REGISTERED" },
            buildingNumber = "12",
            street = $"High Street {suffix}",
            locality = "Testville",
            postalCode = "AB1 2CD",
            countryIso = "GB",
        };
        var blank = new
        {
            addressTypes = new[] { "REGISTERED" },
            buildingNumber = "12",
            street = $"High Street {suffix}",
            locality = "Testville",
            region = "  ",
            postalCode = "AB1 2CD",
            countryIso = "GB",
        };

        var first = await PostAsync(Ltd(absent));
        var second = await PostAsync(Ltd(blank));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstStored = await ReadStoredAsync((await ReadOrganisationAsync(first)).Id);
        var secondStored = await ReadStoredAsync((await ReadOrganisationAsync(second)).Id);
        secondStored.Addresses.Single().AddressId.Should().Be(firstStored.Addresses.Single().AddressId);
        secondStored.Addresses.Single().Address.Region.Should().BeNull();
        (await CountAddressesAsync(suffix)).Should().Be(1);
    }

    // The match is exact over the nine parts: one part more, one part different or a difference
    // in case alone makes another address, stored as a second row
    [Theory]
    [InlineData("street in upper case")]
    [InlineData("other postal code")]
    [InlineData("building name added")]
    [InlineData("region added")]
    public async Task AddressDifferingInOnePart_ShouldBeStoredAsSecondRow(string difference)
    {
        var suffix = NewSuffix();
        var variant = difference switch
        {
            "street in upper case" => VariantAddress(street: $"HIGH STREET {suffix}"),
            "other postal code" => VariantAddress(street: $"High Street {suffix}", postalCode: "AB1 2CE"),
            "building name added" => VariantAddress(street: $"High Street {suffix}", buildingName: "Mill House"),
            "region added" => VariantAddress(street: $"High Street {suffix}", region: "Testshire"),
            _ => throw new ArgumentOutOfRangeException(nameof(difference), difference, null),
        };

        var first = await PostAsync(Ltd(Address(suffix, "REGISTERED")));
        var second = await PostAsync(Ltd(variant));
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

    private static string RequiredKind(bool registeredAtCompaniesHouse) =>
        registeredAtCompaniesHouse ? "REGISTERED" : "PRINCIPAL";

    private static object Address(string suffix, params string[] addressTypes) =>
        new
        {
            addressTypes,
            buildingNumber = "12",
            street = $"High Street {suffix}",
            locality = "Testville",
            postalCode = "AB1 2CD",
            countryIso = "GB",
        };

    // Address(suffix, "REGISTERED") with one part changed, added or removed
    private static object VariantAddress(
        string street,
        string? buildingName = null,
        string? region = null,
        string postalCode = "AB1 2CD"
    ) =>
        new
        {
            addressTypes = new[] { "REGISTERED" },
            buildingName,
            buildingNumber = "12",
            street,
            locality = "Testville",
            region,
            postalCode,
            countryIso = "GB",
        };

    private static object Ltd(params object[] addresses) =>
        new
        {
            organisation = new
            {
                name = "Acme Waste Ltd",
                companiesHouseNumber = "12345678",
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
            a => a.Street != null && a.Street.Contains(suffix),
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
