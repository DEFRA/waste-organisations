namespace Defra.WasteOrganisations.Api.Utils.Metrics;

public interface IRequestMetrics
{
    void RequestCompleted(string requestPath, string httpMethod, int statusCode, double milliseconds);
    void RequestFaulted(string requestPath, string httpMethod, int statusCode, Exception exception);
}
