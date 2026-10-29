using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Http;

namespace Defra.WasteOrganisations.Api.Tests.Versioning;

public class ApiVersionsMetadataTests
{
    [Fact]
    public void WhenNoVersionIsDeclared_ShouldThrow()
    {
        var act = () => new ApiVersionsMetadata();

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhenAVersionIsBelow1_ShouldThrow(int version)
    {
        var act = () => new ApiVersionsMetadata(1, version);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_WhenTheEndpointDeclaresNothing_ShouldBeVersion1Only()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "plain");

        ApiVersionsMetadata.For(endpoint).Should().Equal(1);
    }

    [Fact]
    public void For_WhenTheEndpointDeclaresVersions_ShouldBeThoseVersions()
    {
        var metadata = new EndpointMetadataCollection(new ApiVersionsMetadata(1, 2));
        var endpoint = new Endpoint(_ => Task.CompletedTask, metadata, "versioned");

        ApiVersionsMetadata.For(endpoint).Order().Should().Equal(1, 2);
    }
}
