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

    private enum FixtureType
    {
        Value1,
    }
}
