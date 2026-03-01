using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ghost.Monitoring;

/// <summary>
/// Configuration for OpenTelemetry distributed tracing.
/// </summary>
public static class OpenTelemetryConfiguration
{
    /// <summary>
    /// Activity source name for Ghost operations.
    /// </summary>
    public const string ActivitySourceName = "Ghost";

    /// <summary>
    /// Activity source for manual instrumentation.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// Configures OpenTelemetry distributed tracing.
    /// </summary>
    public static IServiceCollection AddGhostOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection telemetryConfig = configuration.GetSection("Ghost:Monitoring:Telemetry");
        bool exportTraces = telemetryConfig.GetValue("ExportTraces", false);
        bool exportMetrics = telemetryConfig.GetValue("ExportMetrics", false);
        string? otlpEndpoint = telemetryConfig.GetValue<string?>("OtlpEndpoint");
        string serviceName = configuration.GetValue("Ghost:ServiceName", "Ghost");

        if (!exportTraces && !exportMetrics)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: serviceName,
                    serviceVersion: typeof(OpenTelemetryConfiguration).Assembly.GetName().Version?.ToString() ?? "1.0.0");

                // Add custom attributes from configuration
                IEnumerable<IConfigurationSection> customAttributes = telemetryConfig.GetSection("CustomAttributes").GetChildren();
                foreach (IConfigurationSection attribute in customAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(attribute.Value))
                    {
                        resource.AddAttributes(new Dictionary<string, object>
                        {
                            [attribute.Key] = attribute.Value
                        });
                    }
                }
            })
            .WithTracing(tracing =>
            {
                if (!exportTraces)
                {
                    return;
                }

                tracing
                    .AddSource(ActivitySourceName)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Don't trace health check endpoints to reduce noise
                            string path = httpContext.Request.Path.Value ?? string.Empty;
                            return !path.Contains("/health", StringComparison.OrdinalIgnoreCase);
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = request =>
                        {
                            // Don't trace health check requests
                            string uri = request.RequestUri?.ToString() ?? string.Empty;
                            return !uri.Contains("/health", StringComparison.OrdinalIgnoreCase);
                        };
                    });

                // Add OTLP exporter if endpoint is configured
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            });

        return services;
    }
}
