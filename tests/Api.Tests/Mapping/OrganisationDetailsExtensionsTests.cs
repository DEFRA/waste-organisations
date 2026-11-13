using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Mapping;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.Tests.Mapping;

public class OrganisationDetailsExtensionsTests
{
    private static readonly Guid OrganisationTypeId = Guid.NewGuid();

    [Fact]
    public void ToDetailsResponse_ShouldHoldTheOrganisationAsTheOneItem()
    {
        var organisation = StoredOrganisation((StoredAddress("12A", "High Street"), "REGISTERED"));
        organisation.TradingName = "Acme";

        var response = organisation.ToDetailsResponse();

        var item = response.Organisations.Should().ContainSingle().Subject;
        item.Id.Should().Be(organisation.Id);
        item.Name.Should().Be("Acme Waste Ltd");
        item.TradingName.Should().Be("Acme");
        item.CompaniesHouseNumber.Should().Be("12345678");
        item.RegulatorNation.Should().Be(RegulatorNation.England);
        item.OrganisationType.Should().Be("LTD");
        item.ValidTo.Should().BeNull();
        var address = item.Addresses.Should().ContainSingle().Subject;
        address.AddressTypes.Should().Equal(AddressType.Registered);
        address.AddressLine.Should().Be("12A High Street, Testville, AB1 2CD");
        address.BuildingNumber.Should().Be("12A");
        address.Street.Should().Be("High Street");
        address.Locality.Should().Be("Testville");
        address.PostalCode.Should().Be("AB1 2CD");
        address.CountryIso.Should().Be("GB");
    }

    [Fact]
    public void ToDetailsResponse_WithTwoAddresses_ShouldGiveTwoItems()
    {
        var shared = StoredAddress("12A", "High Street");
        var other = StoredAddress("12", "Harbour Road");
        var organisation = StoredOrganisation((shared, "REGISTERED"), (other, "CORRESPONDENCE"), (shared, "PRINCIPAL"));

        var addresses = organisation.ToDetailsResponse().Organisations[0].Addresses;

        addresses.Should().HaveCount(2);
        addresses
            .Should()
            .ContainSingle(address => address.Street == "High Street")
            .Which.AddressTypes.Should()
            .BeEquivalentTo([AddressType.Registered, AddressType.Principal]);
        addresses
            .Should()
            .ContainSingle(address => address.Street == "Harbour Road")
            .Which.AddressTypes.Should()
            .Equal(AddressType.Correspondence);
    }

    [Fact]
    public void ToDetailsResponse_WithNoLinks_ShouldGiveAnEmptyAddressArray()
    {
        var organisation = StoredOrganisation();

        organisation.ToDetailsResponse().Organisations[0].Addresses.Should().BeEmpty();
    }

    [Fact]
    public void ToDetailsResponse_WithKindsOfOneAddressInSeparateInstances_ShouldGiveOneItem()
    {
        var registered = StoredAddress("12A", "High Street");
        var principal = StoredAddress("12A", "High Street");
        principal.Id = registered.Id;
        var organisation = StoredOrganisation((registered, "REGISTERED"), (principal, "PRINCIPAL"));
        organisation.Addresses[0].ValidFrom = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        organisation.Addresses[1].ValidFrom = new DateTime(2026, 11, 5, 14, 30, 0, DateTimeKind.Utc);

        var addresses = organisation.ToDetailsResponse().Organisations[0].Addresses;

        addresses
            .Should()
            .ContainSingle()
            .Which.AddressTypes.Should()
            .BeEquivalentTo([AddressType.Registered, AddressType.Principal]);
    }

    [Fact]
    public void ToDetailsResponse_WhenSerialised_ShouldOmitNullMembers()
    {
        var address = new Address
        {
            Id = Guid.CreateVersion7(),
            BuildingNumber = "12",
            Street = "Harbour Road",
            PostalCode = "AB1 2CD",
        };
        var organisation = StoredOrganisation((address, "PRINCIPAL"));
        organisation.OrganisationType = null;
        organisation.OrganisationTypeId = null;

        var json = JsonSerializer.SerializeToElement(organisation.ToDetailsResponse());

        var body = json.GetProperty("organisations")[0];
        body.TryGetProperty("validTo", out _).Should().BeFalse();
        body.TryGetProperty("organisationType", out _).Should().BeFalse();
        body.TryGetProperty("tradingName", out _).Should().BeFalse();
        body.GetProperty("name").GetString().Should().Be("Acme Waste Ltd");

        var item = body.GetProperty("addresses")[0];
        foreach (var absent in new[] { "subBuildingName", "buildingName", "poBox", "locality", "region", "countryIso" })
            item.TryGetProperty(absent, out _).Should().BeFalse(absent);
        item.GetProperty("street").GetString().Should().Be("Harbour Road");
        item.GetProperty("addressTypes")[0].GetString().Should().Be("PRINCIPAL");
    }

    [Fact]
    public void ToDetailsResponse_WithAnUnspecifiedValidTo_ShouldGiveAUtcOffset()
    {
        var organisation = StoredOrganisation((StoredAddress("12A", "High Street"), "REGISTERED"));
        organisation.ValidTo = new DateTime(2026, 7, 2, 10, 15, 30, DateTimeKind.Unspecified);

        var validTo = organisation.ToDetailsResponse().Organisations[0].ValidTo;

        validTo.Should().Be(new DateTimeOffset(2026, 7, 2, 10, 15, 30, TimeSpan.Zero));
        validTo.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ToDetailsResponse_WhenSerialised_ShouldCarryNoOrganisationOrAddressDates()
    {
        var registered = StoredAddress("12A", "High Street");
        var correspondence = StoredAddress("12", "Harbour Road");
        var organisation = StoredOrganisation((registered, "REGISTERED"), (correspondence, "CORRESPONDENCE"));
        var stamp = new DateTime(2026, 11, 2, 10, 15, 30, DateTimeKind.Utc);
        organisation.ValidFrom = stamp;
        organisation.CreatedAt = stamp;
        organisation.UpdatedAt = stamp;
        foreach (var link in organisation.Addresses)
        {
            link.ValidFrom = stamp;
            link.ValidTo = stamp.AddDays(1);
            link.CreatedAt = stamp;
            link.UpdatedAt = stamp;
        }

        var json = JsonSerializer.SerializeToElement(organisation.ToDetailsResponse());

        var body = json.GetProperty("organisations")[0];
        body.TryGetProperty("validFrom", out _).Should().BeFalse();
        body.TryGetProperty("createdAt", out _).Should().BeFalse();
        body.TryGetProperty("updatedAt", out _).Should().BeFalse();
        var items = body.GetProperty("addresses").EnumerateArray().ToArray();
        items.Should().HaveCount(2);
        foreach (var item in items)
        {
            item.TryGetProperty("validFrom", out _).Should().BeFalse();
            item.TryGetProperty("validTo", out _).Should().BeFalse();
        }
    }

    private static Address StoredAddress(string buildingNumber, string street) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            BuildingNumber = buildingNumber,
            Street = street,
            Locality = "Testville",
            PostalCode = "AB1 2CD",
            CountryIso = "GB",
        };

    private static Organisation StoredOrganisation(params (Address Address, string UsageType)[] links)
    {
        var organisation = new Organisation
        {
            Id = Guid.CreateVersion7(),
            Name = "Acme Waste Ltd",
            CompaniesHouseNumber = "12345678",
            RegulatorNation = "GB-ENG",
            OrganisationTypeId = OrganisationTypeId,
            OrganisationType = new OrganisationType { Id = OrganisationTypeId, Code = "LTD" },
        };

        foreach (var (address, usageType) in links)
            organisation.Addresses.Add(
                new OrganisationAddress
                {
                    Id = Guid.CreateVersion7(),
                    Organisation = organisation,
                    Address = address,
                    AddressId = address.Id,
                    UsageType = new AddressUsageType { Id = Guid.NewGuid(), Name = usageType },
                }
            );

        return organisation;
    }
}
