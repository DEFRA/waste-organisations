using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace Defra.WasteOrganisations.Api.Versioning;

/// <summary>
/// Resolves the requested API version from the `Accept` header. Only our own media type, with a
/// `version` parameter, can move a request off v1 - see specs/0004-REGEN-167.
/// </summary>
public static class AcceptHeaderVersionResolver
{
    public const string MediaType = "application/vnd.defra.waste-organisations+json";
    public const int DefaultVersion = 1;

    private const string VersionParameterName = "version";

    public static bool TryResolve(string? acceptHeaderValue, out int version) =>
        TryResolve([acceptHeaderValue], out version);

    public static bool TryResolve(IEnumerable<string?> acceptHeaderValues, out int version)
    {
        version = DefaultVersion;

        var values = acceptHeaderValues.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToArray();

        if (values.Length == 0)
            return true;

        if (!MediaTypeHeaderValue.TryParseList(values, out var mediaTypes))
            return true;

        var ourMediaTypes = mediaTypes.Where(IsOurMediaType).ToArray();

        if (ourMediaTypes.Length == 0)
            return true;

        var requestedVersions = new HashSet<int>();

        foreach (var mediaType in ourMediaTypes)
        {
            if (!TryGetVersion(mediaType, out var requestedVersion))
                return false;

            requestedVersions.Add(requestedVersion);
        }

        if (requestedVersions.Count != 1)
            return false;

        version = requestedVersions.Single();

        return true;
    }

    private static bool IsOurMediaType(MediaTypeHeaderValue mediaType) =>
        mediaType.MediaType.Equals(MediaType, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetVersion(MediaTypeHeaderValue mediaType, out int version)
    {
        version = DefaultVersion;

        var parameter = mediaType.Parameters.FirstOrDefault(p =>
            p.Name.Equals(VersionParameterName, StringComparison.OrdinalIgnoreCase)
        );

        if (parameter is null)
            return true;

        var value = parameter.Value.ToString().Trim('"');

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out version) && version >= 1;
    }
}
