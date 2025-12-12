using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;

namespace Defra.WasteOrganisations.Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public class RequestMetrics : IRequestMetrics
{
    private readonly Counter<long> _requestsReceived;
    private readonly Counter<long> _requestsFaulted;
    private readonly Histogram<double> _requestDuration;

    public RequestMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.Names.MeterName);

        _requestsReceived = meter.CreateCounter<long>(
            "RequestReceived",
            nameof(Unit.COUNT),
            "Count of messages received"
        );
        _requestsFaulted = meter.CreateCounter<long>("RequestFaulted", nameof(Unit.COUNT), "Count of request faults");
        _requestDuration = meter.CreateHistogram<double>(
            "RequestDuration",
            nameof(Unit.MILLISECONDS),
            "Duration of request"
        );
    }

    public void RequestCompleted(string requestPath, string httpMethod, int statusCode, double milliseconds)
    {
        var tagList = BuildTags(requestPath, httpMethod, statusCode);

        _requestsReceived.Add(1, tagList);
        _requestDuration.Record(milliseconds, tagList);
    }

    public void RequestFaulted(string requestPath, string httpMethod, int statusCode, Exception exception)
    {
        var tagList = BuildTags(requestPath, httpMethod, statusCode);

        tagList.Add(Metrics.Tags.ExceptionType, exception.GetType().Name);

        _requestsFaulted.Add(1, tagList);
    }

    private static TagList BuildTags(string requestPath, string httpMethod, int statusCode) =>
        new()
        {
            { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName },
            { Metrics.Tags.RequestPath, requestPath },
            { Metrics.Tags.HttpMethod, httpMethod },
            { Metrics.Tags.StatusCode, statusCode },
        };
}
