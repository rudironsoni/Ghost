using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ghost.Cloud.Api.Observability;

public static class CloudApiTelemetry
{
    public const string ActivitySourceName = "Ghost.Cloud.Api";
    public const string GrainsActivitySourceName = "Ghost.Cloud.Grains";
    public const string MeterName = "Ghost.Cloud.Api";
    public const string GrainsMeterName = "Ghost.Cloud.Grains";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> RunTriggerRequestsCounter = Meter.CreateCounter<long>(
        "ghost_cloud_run_trigger_requests_total",
        unit: "requests");

    private static readonly Counter<long> RunTriggerFailuresCounter = Meter.CreateCounter<long>(
        "ghost_cloud_run_trigger_failures_total",
        unit: "requests");

    private static readonly Counter<long> CanaryDispatchCounter = Meter.CreateCounter<long>(
        "ghost_cloud_canary_dispatch_total",
        unit: "runs");

    private static readonly Counter<long> CanaryDispatchFailuresCounter = Meter.CreateCounter<long>(
        "ghost_cloud_canary_dispatch_failures_total",
        unit: "runs");

    private static readonly Histogram<double> CanaryDurationHistogram = Meter.CreateHistogram<double>(
        "ghost_cloud_canary_duration_seconds",
        unit: "s");

    public static IServiceCollection AddCloudObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string serviceName = configuration.GetValue("Ghost:ServiceName", "ghost-cloud-api");
        string? otlpEndpoint = configuration.GetValue<string>("Cloud:Observability:OtlpEndpoint")
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        bool exportConsole = configuration.GetValue("Cloud:Observability:ConsoleExporter", true);

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: serviceName,
                    serviceVersion: typeof(CloudApiTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                    .AddHttpClientInstrumentation(options => options.RecordException = true)
                    .AddSource(ActivitySourceName)
                    .AddSource(GrainsActivitySourceName);

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
                }
                else if (exportConsole)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(MeterName)
                    .AddMeter(GrainsMeterName);

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
                }
                else if (exportConsole)
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }

    public static void RecordRunTriggerRequest(string mode)
    {
        RunTriggerRequestsCounter.Add(1, new KeyValuePair<string, object?>("mode", mode));
    }

    public static void RecordRunTriggerFailure(string reason, string mode)
    {
        RunTriggerFailuresCounter.Add(
            1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("mode", mode));
    }

    public static void RecordCanaryDispatch(string provider, string status)
    {
        CanaryDispatchCounter.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("status", status));
    }

    public static void RecordCanaryDispatchFailure(string provider, string reason)
    {
        CanaryDispatchFailuresCounter.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("reason", reason));
    }

    public static void RecordCanaryDuration(string provider, double durationSeconds)
    {
        CanaryDurationHistogram.Record(
            durationSeconds,
            new KeyValuePair<string, object?>("provider", provider));
    }
}
