namespace Defra.WasteOrganisations.Api.Versioning;

public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    /// Declares which API versions this endpoint supports. An endpoint that does not call this
    /// supports v1 only.
    /// </summary>
    public static TBuilder WithApiVersions<TBuilder>(this TBuilder builder, params int[] versions)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new ApiVersionsMetadata(versions));

        return builder;
    }
}
