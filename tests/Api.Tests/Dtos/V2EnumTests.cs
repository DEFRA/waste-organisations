using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;

namespace Defra.WasteOrganisations.Api.Tests.Dtos;

// The wire names are the v2 contract, and AddressType's are also the address_usagetype names the
// create looks up, so a renamed member must not change what is sent or stored
public class V2EnumTests
{
    [Theory]
    [InlineData(AddressType.Registered, "REGISTERED")]
    [InlineData(AddressType.Principal, "PRINCIPAL")]
    [InlineData(AddressType.Audit, "AUDIT")]
    [InlineData(AddressType.ServiceOfNotice, "SERVICE_OF_NOTICE")]
    [InlineData(AddressType.Correspondence, "CORRESPONDENCE")]
    public void AddressType_ShouldRoundTripWithItsWireName(AddressType value, string wireName)
    {
        JsonSerializer.Serialize(value).Should().Be($"\"{wireName}\"");
        JsonSerializer.Deserialize<AddressType>($"\"{wireName}\"").Should().Be(value);
    }

    // Integers and numeric strings would bind a value no usage type has (AddressTypeJsonConverter)
    [Theory]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("\"0\"")]
    [InlineData("\"999\"")]
    public void AddressType_ShouldNotReadANumber(string json)
    {
        var read = () => JsonSerializer.Deserialize<AddressType>(json);

        read.Should().Throw<JsonException>();
    }

    [Fact]
    public void AddressType_ToJsonValue_ShouldGiveTheUsageTypeName()
    {
        AddressType.ServiceOfNotice.ToJsonValue().Should().Be("SERVICE_OF_NOTICE");
    }

    [Theory]
    [InlineData(RegulatorNation.England, "GB-ENG")]
    [InlineData(RegulatorNation.Scotland, "GB-SCT")]
    [InlineData(RegulatorNation.Wales, "GB-WLS")]
    [InlineData(RegulatorNation.NorthernIreland, "GB-NIR")]
    public void RegulatorNation_ShouldRoundTripWithItsWireName(RegulatorNation value, string wireName)
    {
        JsonSerializer.Serialize(value).Should().Be($"\"{wireName}\"");
        JsonSerializer.Deserialize<RegulatorNation>($"\"{wireName}\"").Should().Be(value);
    }
}
