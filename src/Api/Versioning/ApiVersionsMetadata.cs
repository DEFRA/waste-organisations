namespace Defra.WasteOrganisations.Api.Versioning;

/// <summary>
/// Endpoint metadata declaring which API versions an endpoint supports. An endpoint with no such
/// metadata supports v1 only - see <see cref="EndpointConventionBuilderExtensions.WithApiVersions"/>.
/// </summary>
public sealed class ApiVersionsMetadata
{
    public ApiVersionsMetadata(params int[] versions)
    {
        if (versions.Length == 0)
            throw new ArgumentException("At least one version must be declared.", nameof(versions));

        Versions = versions.ToHashSet();
    }

    public IReadOnlySet<int> Versions { get; }
}
