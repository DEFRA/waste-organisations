using System.Diagnostics.CodeAnalysis;

namespace Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public class MetricsMiddleware(IRequestMetrics requestMetrics) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        var path = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unknown";

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            requestMetrics.RequestFaulted(path, context.Request.Method, context.Response.StatusCode, ex);

            throw;
        }
        finally
        {
            requestMetrics.RequestCompleted(
                path,
                context.Request.Method,
                context.Response.StatusCode,
                TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds
            );
        }
    }
}
