using System.Diagnostics.CodeAnalysis;

namespace Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRequestMetrics(this IServiceCollection services)
    {
        services.AddTransient<MetricsMiddleware>();
        services.AddSingleton<IRequestMetrics, RequestMetrics>();

        return services;
    }
}
