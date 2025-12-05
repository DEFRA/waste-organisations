using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos;
using Api.Extensions;
using AwesomeAssertions;

namespace Api.Tests.Extensions;

public class EnumExtensionsTests
{
    [Fact]
    public void ToJsonString_AsExpected()
    {
        RegistrationType.LargeProducer.ToJsonValue().Should().Be("LARGE_PRODUCER");
    }

    [Fact]
    public void ToJsonString_WhenNoAttribute_ShouldFallbackToValue()
    {
        FixtureType.Value1.ToJsonValue().Should().Be("Value1");
    }

    [Fact]
    public void FromJsonValue_AsExpected()
    {
        "LARGE_PRODUCER".FromJsonValue<RegistrationType>().Should().Be(RegistrationType.LargeProducer);
    }

    [Fact]
    public void FromJsonValue_WhenInvalid_ShouldThrow()
    {
        var act = () => "INVALID".FromJsonValue<RegistrationType>();

        act.Should().Throw<JsonException>();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum FixtureType
    {
        Value1,
    }
}
