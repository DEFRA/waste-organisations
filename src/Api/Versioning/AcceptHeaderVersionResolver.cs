using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace Defra.WasteOrganisations.Api.Versioning;

/// <summary>
/// Resolves the requested API version from the `Accept` header. Only our own media type, with a
/// `version` parameter, can move a request off v1 - see specs/0004-REGEN-167.
/// </summary>
public static class AcceptHeaderVersionResolver
{
    internal const string MediaType = "application/vnd.defra.waste-organisations+json";
    public const int DefaultVersion = 1;

    private const string VersionParameterName = "version";

    /// <summary>
    /// Resolves the version. False when a `version` is malformed or our media type is listed with
    /// different versions; <paramref name="version"/> is then <see cref="DefaultVersion"/>.
    /// </summary>
    public static bool TryResolve(IEnumerable<string?> acceptHeaderValues, out int version)
    {
        // Anything but our own media type is served v1, as is a header that does not parse.
        var values = acceptHeaderValues.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToArray();
        var ourMediaTypes = MediaTypeHeaderValue.TryParseList(values, out var mediaTypes)
            ? mediaTypes.Where(IsOurMediaType)
            : [];

        // Our entries must agree on one well-formed version. An entry without one counts as v1.
        var requestedVersions = ourMediaTypes.Select(RequestedVersion).ToHashSet();
        var resolved = requestedVersions.Count switch
        {
            0 => DefaultVersion,
            1 => requestedVersions.Single(),
            _ => null,
        };

        version = resolved ?? DefaultVersion;

        return resolved is not null;
    }

    private static bool IsOurMediaType(MediaTypeHeaderValue mediaType) =>
        mediaType.MediaType.Equals(MediaType, StringComparison.OrdinalIgnoreCase);

    /// <summary>The entry's version, <see cref="DefaultVersion"/> when it has none, null when malformed.</summary>
    private static int? RequestedVersion(MediaTypeHeaderValue mediaType)
    {
        var parameter = mediaType.Parameters.FirstOrDefault(p =>
            p.Name.Equals(VersionParameterName, StringComparison.OrdinalIgnoreCase)
        );
        var value = parameter?.Value.ToString().Trim('"');

        return value switch
        {
            null => DefaultVersion,
            _ when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
                    && version >= 1 => version,
            _ => null,
        };
    }
}
