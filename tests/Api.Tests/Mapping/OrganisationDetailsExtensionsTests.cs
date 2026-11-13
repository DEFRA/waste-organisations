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
