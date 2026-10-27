using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Versioning;

namespace Defra.WasteOrganisations.Api.Tests.Versioning;

public class AcceptHeaderVersionResolverTests
{
    [Fact]
    public void WhenAbsent_ShouldResolveToVersion1()
    {
        AcceptHeaderVersionResolver.TryResolve((string?)null, out var version).Should().BeTrue();
        version.Should().Be(1);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("*/*")]
    [InlineData("text/html")]
    [InlineData("application/vnd.defra.waste-organisations+json")]
    [InlineData("application/vnd.defra.waste-organisations+json; version=1")]
    public void WhenServedAsV1_ShouldResolveToVersion1(string acceptHeaderValue)
    {
        AcceptHeaderVersionResolver.TryResolve(acceptHeaderValue, out var version).Should().BeTrue();
        version.Should().Be(1);
    }

    [Fact]
    public void WhenVersion2Requested_ShouldResolveToVersion2()
    {
        AcceptHeaderVersionResolver
            .TryResolve("application/vnd.defra.waste-organisations+json; version=2", out var version)
            .Should()
            .BeTrue();
        version.Should().Be(2);
    }

    [Theory]
    [InlineData("application/vnd.defra.waste-organisations+json; version=abc")]
    [InlineData("application/vnd.defra.waste-organisations+json; version=0")]
    [InlineData("application/vnd.defra.waste-organisations+json; version=-1")]
    [InlineData("application/vnd.defra.waste-organisations+json; version=1.1")]
    [InlineData("application/vnd.defra.waste-organisations+json; version=")]
    public void WhenMalformed_ShouldFailToResolve(string acceptHeaderValue)
    {
        AcceptHeaderVersionResolver.TryResolve(acceptHeaderValue, out _).Should().BeFalse();
    }

    [Fact]
    public void WhenListedTwiceWithDifferentVersions_ShouldFailToResolve_Ambiguous()
    {
        AcceptHeaderVersionResolver
            .TryResolve(
                [
                    "application/vnd.defra.waste-organisations+json; version=1",
                    "application/vnd.defra.waste-organisations+json; version=2",
                ],
                out _
            )
            .Should()
            .BeFalse();
    }

    [Fact]
    public void WhenListedTwiceWithSameVersion_ShouldResolveToThatVersion()
    {
        AcceptHeaderVersionResolver
            .TryResolve(
                [
                    "application/vnd.defra.waste-organisations+json",
                    "application/vnd.defra.waste-organisations+json; version=1",
                ],
                out var version
            )
            .Should()
            .BeTrue();
        version.Should().Be(1);
    }

    [Fact]
    public void WhenOurMediaTypeBesideOtherTypes_ShouldOnlyConsiderOurs()
    {
        AcceptHeaderVersionResolver
            .TryResolve(["text/html", "application/vnd.defra.waste-organisations+json; version=2"], out var version)
            .Should()
            .BeTrue();
        version.Should().Be(2);
    }
}
