using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Mapping;

namespace Defra.WasteOrganisations.Api.Tests.Mapping;

// The order is postal order (refinement answer, 2 November 2026), not the Remapping table's, so a
// PO box address keeps the part that identifies it; the country is never part of the line
public class AddressLineTests
{
    [Fact]
    public void Build_WithEveryPart_ShouldJoinInPostalOrder()
    {
        var address = new Address
        {
            SubBuildingName = "Unit 4",
            BuildingName = "Mill House",
            BuildingNumber = "12A",
            Street = "High Street",
            Locality = "Testville",
            Region = "Testshire",
            PostalCode = "AB1 2CD",
            PoBox = "PO Box 99",
            CountryIso = "GB",
        };

        AddressLine
            .Build(address)
            .Should()
            .Be("Unit 4, Mill House, 12A High Street, PO Box 99, Testville, Testshire, AB1 2CD");
    }

    [Fact]
    public void Build_WithNumberAndStreetOnly_ShouldJoinThemWithOneSpace()
    {
        var address = new Address { BuildingNumber = "12", Street = "High Street" };

        AddressLine.Build(address).Should().Be("12 High Street");
    }

    [Fact]
    public void Build_WithNamedBuildingOnly_ShouldReturnTheName()
    {
        var address = new Address { BuildingName = "Rose Farm", CountryIso = "GB" };

        AddressLine.Build(address).Should().Be("Rose Farm");
    }

    [Fact]
    public void Build_WithNamedBuildingWithoutStreet_ShouldSkipTheStreet()
    {
        var address = new Address
        {
            BuildingName = "Rose Farm",
            Locality = "Little Testing",
            PostalCode = "AB1 2CD",
            CountryIso = "GB",
        };

        AddressLine.Build(address).Should().Be("Rose Farm, Little Testing, AB1 2CD");
    }

    [Fact]
    public void Build_WithPoBoxOnlyPremises_ShouldKeepThePoBox()
    {
        var address = new Address
        {
            PoBox = "PO Box 99",
            Locality = "Testville",
            PostalCode = "AB1 9ZZ",
            CountryIso = "GB",
        };

        AddressLine.Build(address).Should().Be("PO Box 99, Testville, AB1 9ZZ");
    }

    [Fact]
    public void Build_WithNonUkAddressWithoutPostcode_ShouldLeaveOutTheCountry()
    {
        var address = new Address
        {
            BuildingNumber = "12",
            Street = "Harbour Road",
            Locality = "Hong Kong",
            CountryIso = "HK",
        };

        AddressLine.Build(address).Should().Be("12 Harbour Road, Hong Kong");
    }

    [Fact]
    public void Build_WithStreetButNoNumber_ShouldNotLeadWithASpace()
    {
        var address = new Address
        {
            BuildingName = "Mill House",
            Street = "High Street",
            Locality = "Testville",
        };

        AddressLine.Build(address).Should().Be("Mill House, High Street, Testville");
    }

    [Fact]
    public void Build_WithBlankParts_ShouldSkipThem()
    {
        var address = new Address
        {
            SubBuildingName = "",
            BuildingName = "   ",
            BuildingNumber = "12",
            Street = " ",
            PoBox = null,
            Locality = "Testville",
            Region = "",
            PostalCode = "AB1 2CD",
            CountryIso = "GB",
        };

        AddressLine.Build(address).Should().Be("12, Testville, AB1 2CD");
    }
}
