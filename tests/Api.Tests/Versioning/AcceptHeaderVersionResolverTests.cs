using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Versioning;

namespace Defra.WasteOrganisations.Api.Tests.Versioning;

public class AcceptHeaderVersionResolverTests
{
    private const string MediaType = "application/vnd.defra.waste-organisations+json";

    [Fact]
    public void WhenAbsent_ShouldResolveToVersion1()
    {
        AcceptHeaderVersionResolver.TryResolve([null], out var version).Should().BeTrue();
        version.Should().Be(1);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("*/*")]
    [InlineData("text/html")]
    [InlineData(MediaType)]
    public void WhenNoVersionRequested_ShouldResolveToVersion1(string acceptHeaderValue)
    {
        AcceptHeaderVersionResolver.TryResolve([acceptHeaderValue], out var version).Should().BeTrue();
        version.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(42)]
    public void WhenVersionIsRequested_ShouldResolveToThatVersion(int requestedVersion)
    {
        AcceptHeaderVersionResolver
            .TryResolve([$"{MediaType}; version={requestedVersion}"], out var version)
            .Should()
            .BeTrue();
        version.Should().Be(requestedVersion);
    }

    [Theory]
    [InlineData($"{MediaType}; version=abc")]
    [InlineData($"{MediaType}; version=0")]
    [InlineData($"{MediaType}; version=-1")]
    [InlineData($"{MediaType}; version=1.1")]
    [InlineData($"{MediaType}; version=")]
    public void WhenMalformed_ShouldFailToResolve(string acceptHeaderValue)
    {
        AcceptHeaderVersionResolver.TryResolve([acceptHeaderValue], out _).Should().BeFalse();
    }

    [Fact]
    public void WhenListedTwiceWithDifferentVersions_ShouldFailToResolve()
    {
        AcceptHeaderVersionResolver
            .TryResolve([$"{MediaType}; version=1", $"{MediaType}; version=2"], out _)
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void WhenListedTwiceWithTheSameVersion_ShouldResolveToThatVersion(int requestedVersion)
    {
        AcceptHeaderVersionResolver
            .TryResolve(
                [$"{MediaType}; version={requestedVersion}", $"{MediaType}; version={requestedVersion}"],
                out var version
            )
            .Should()
            .BeTrue();
        version.Should().Be(requestedVersion);
    }

    // Our media type without a version counts as version 1 (the resolution table in the spec).
    [Fact]
    public void WhenListedWithoutVersionBesideVersion1_ShouldResolveToVersion1()
    {
        AcceptHeaderVersionResolver
            .TryResolve([MediaType, $"{MediaType}; version=1"], out var version)
            .Should()
            .BeTrue();
        version.Should().Be(1);
    }

    [Fact]
    public void WhenListedWithoutVersionBesideAnotherVersion_ShouldFailToResolve()
    {
        AcceptHeaderVersionResolver.TryResolve([MediaType, $"{MediaType}; version=2"], out _).Should().BeFalse();
    }

    [Fact]
    public void WhenOurMediaTypeBesideOtherTypes_ShouldOnlyConsiderOurs()
    {
        AcceptHeaderVersionResolver
            .TryResolve(["text/html", $"{MediaType}; version=2"], out var version)
            .Should()
            .BeTrue();
        version.Should().Be(2);
    }
}
