using System.Diagnostics.CodeAnalysis;

namespace Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public static class Metrics
{
    public static class Names
    {
        public const string MeterName = "Defra.WasteOrganisationsApi";
    }

    public static class Tags
    {
        public const string Service = nameof(Service);
        public const string HttpMethod = nameof(HttpMethod);
        public const string RequestPath = nameof(RequestPath);
        public const string StatusCode = nameof(StatusCode);
        public const string ExceptionType = nameof(ExceptionType);
    }
}
