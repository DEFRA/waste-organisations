namespace Defra.WasteOrganisations.Api.Versioning;

/// <summary>
/// Endpoint metadata declaring which API versions an endpoint supports. An endpoint with no such
/// metadata supports v1 only - see <see cref="EndpointConventionBuilderExtensions.WithApiVersions"/>.
/// </summary>
public sealed class ApiVersionsMetadata
{
    /// <summary>The versions an endpoint without this metadata supports: v1 only.</summary>
    public static IReadOnlySet<int> Default { get; } = new HashSet<int> { AcceptHeaderVersionResolver.DefaultVersion };

    public ApiVersionsMetadata(params int[] versions)
    {
        if (versions.Length == 0)
            throw new ArgumentException("At least one version must be declared.", nameof(versions));

        if (Array.Exists(versions, version => version < AcceptHeaderVersionResolver.DefaultVersion))
            throw new ArgumentException("Versions start at 1.", nameof(versions));

        Versions = versions.ToHashSet();
    }

    public IReadOnlySet<int> Versions { get; }

    /// <summary>The versions an endpoint supports: those it declares, or <see cref="Default"/>.</summary>
    public static IReadOnlySet<int> For(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ApiVersionsMetadata>()?.Versions ?? Default;
}
