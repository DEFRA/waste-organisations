using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace Defra.WasteOrganisations.Api.Tests.Versioning;

/// <summary>
/// The routing tree the policy builds for one path: which endpoints each version sees, and which
/// branch a request takes. <see cref="ApiVersionMatcherPolicyRoutingTests"/> covers the result over
/// HTTP.
/// </summary>
public class ApiVersionMatcherPolicyTests
{
    private const string MediaType = "application/vnd.defra.waste-organisations+json";

    private static readonly Endpoint s_getV1 = Create("get v1", "GET");
    private static readonly Endpoint s_getV2 = Create("get v2", "GET", 2);
    private static readonly Endpoint s_putV1 = Create("put v1", "PUT");
    private static readonly Endpoint s_postV2 = Create("post v2", "POST", 2);

    private readonly ApiVersionMatcherPolicy _subject = new();

    [Fact]
    public void Order_ShouldBeBeforeTheHttpMethodPolicy_SoThe405IsPerVersion() =>
        _subject.Order.Should().BeLessThan(new HttpMethodMatcherPolicy().Order);

    [Fact]
    public void AppliesToEndpoints_WhenNoEndpointDeclaresVersions_ShouldBeFalse() =>
        _subject.AppliesToEndpoints([s_getV1, s_putV1]).Should().BeFalse();

    [Fact]
    public void AppliesToEndpoints_WhenAnEndpointDeclaresVersions_ShouldBeTrue() =>
        _subject.AppliesToEndpoints([s_getV1, s_getV2]).Should().BeTrue();

    [Fact]
    public void GetEdges_ShouldKeepV1Strict_AndGiveOtherVersionsTheV1EndpointsForMethodsTheyLack()
    {
        var edges = Edges([s_getV1, s_getV2, s_putV1, s_postV2]);

        edges.Keys.Order().Should().Equal(0, 1, 2);
        // v1 never sees a v2-only endpoint, so it keeps its 405 for POST.
        edges[1].Should().BeEquivalentTo("get v1", "put v1");
        // v2 gets its own GET and POST, and falls back to the v1 PUT (which the middleware turns into 406).
        edges[2].Should().BeEquivalentTo("get v2", "post v2", "put v1");
        // An unknown version takes the v1 endpoints plus the v2-only POST, so every method reaches the middleware.
        edges[0].Should().BeEquivalentTo("get v1", "put v1", "post v2");
    }

    [Fact]
    public void GetEdges_WhenOnlyV2IsDeclared_ShouldLeaveV1WithNothing()
    {
        var edges = Edges([s_getV2]);

        edges[1].Should().BeEmpty();
        edges[2].Should().BeEquivalentTo("get v2");
        edges[0].Should().BeEquivalentTo("get v2");
    }

    [Theory]
    [InlineData(null, 11)]
    [InlineData("application/json", 11)]
    [InlineData($"{MediaType}; version=1", 11)]
    [InlineData($"{MediaType}; version=2", 12)]
    [InlineData($"{MediaType}; version=3", 10)]
    [InlineData($"{MediaType}; version=abc", 10)]
    public void JumpTable_ShouldFollowTheRequestedVersion_OrTheOtherVersionsBranch(string? accept, int expected)
    {
        var table = _subject.BuildJumpTable(
            exitDestination: 99,
            [new PolicyJumpTableEdge(0, 10), new PolicyJumpTableEdge(1, 11), new PolicyJumpTableEdge(2, 12)]
        );
        var context = new DefaultHttpContext();

        if (accept is not null)
            context.Request.Headers.Accept = accept;

        table.GetDestination(context).Should().Be(expected);
    }

    private Dictionary<int, IEnumerable<string?>> Edges(Endpoint[] endpoints) =>
        _subject
            .GetEdges(endpoints)
            .ToDictionary(e => (int)e.State, e => e.Endpoints.Select(endpoint => endpoint.DisplayName));

    private static Endpoint Create(string name, string method, params int[] versions)
    {
        var metadata =
            versions.Length == 0
                ? new EndpointMetadataCollection(new HttpMethodMetadata([method]))
                : new EndpointMetadataCollection(new HttpMethodMetadata([method]), new ApiVersionsMetadata(versions));

        return new Endpoint(_ => Task.CompletedTask, metadata, name);
    }
}
