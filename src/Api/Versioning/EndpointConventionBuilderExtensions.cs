namespace Defra.WasteOrganisations.Api.Versioning;

public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    /// Declares which API versions this endpoint supports. An endpoint that does not call this
    /// supports v1 only. Two endpoints may share a path and HTTP method when they declare different
    /// versions: <see cref="ApiVersionMatcherPolicy"/> routes each request to the one for its version.
    /// </summary>
    public static TBuilder WithApiVersions<TBuilder>(this TBuilder builder, params int[] versions)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new ApiVersionsMetadata(versions));

        return builder;
    }
}
