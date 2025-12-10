using System.Diagnostics.CodeAnalysis;

namespace Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<MetricsMiddleware>();

        var config = builder.ApplicationServices.GetRequiredService<IConfiguration>();
        var enabled = config.GetValue("AWS_EMF_ENABLED", true);

        if (!enabled)
            return builder;

        var ns = config.GetValue<string>("AWS_EMF_NAMESPACE");
        var env = config.GetValue<string>("AWS_EMF_ENVIRONMENT") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(ns) && env.Equals("Local"))
            ns = typeof(Program).Namespace ?? nameof(Program);

        if (string.IsNullOrWhiteSpace(ns))
            throw new InvalidOperationException("AWS_EMF_NAMESPACE is not set but metrics are enabled");

        MetricsExporter.Init(builder.ApplicationServices.GetRequiredService<ILoggerFactory>(), ns);

        return builder;
    }
}
