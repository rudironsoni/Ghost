using Ghost.Core.Monitoring;
using Ghost.WebApi.Security;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.WebApi.Features.Health;

/// <summary>
/// Prometheus-style metrics endpoints for monitoring.
/// </summary>
public static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/metrics")
            .WithTags("Metrics");

        group.MapGet("/", GetMetrics)
            .WithName("GetMetrics")
            .WithSummary("Get all metrics in JSON format")
            .AddEndpointFilter<AdminApiKeyEndpointFilter>();

        group.MapGet("/prometheus", GetPrometheusMetrics)
            .WithName("GetPrometheusMetrics")
            .WithSummary("Get metrics in Prometheus exposition format")
            .AddEndpointFilter<AdminApiKeyEndpointFilter>();
    }

    private static IResult GetMetrics([FromServices] IMetricsCollector metrics)
    {
        MetricsSnapshot snapshot = metrics.GetSnapshot();
        return Results.Ok(snapshot);
    }

    private static IResult GetPrometheusMetrics([FromServices] IMetricsCollector metrics)
    {
        MetricsSnapshot snapshot = metrics.GetSnapshot();
        var lines = new List<string>();

        lines.Add("# HELP ghost_scrape_attempts Total number of scrape attempts");
        lines.Add("# TYPE ghost_scrape_attempts counter");
        foreach ((string? platform, PlatformMetricsSnapshot? pm) in snapshot.Platforms)
        {
            lines.Add($"ghost_scrape_attempts{{platform=\"{platform}\"}} {pm.ScrapeAttempts}");
        }

        lines.Add("# HELP ghost_scrape_successes Total number of successful scrapes");
        lines.Add("# TYPE ghost_scrape_successes counter");
        foreach ((string? platform, PlatformMetricsSnapshot? pm) in snapshot.Platforms)
        {
            lines.Add($"ghost_scrape_successes{{platform=\"{platform}\"}} {pm.ScrapeSuccesses}");
        }

        lines.Add("# HELP ghost_scrape_failures Total number of failed scrapes");
        lines.Add("# TYPE ghost_scrape_failures counter");
        foreach ((string? platform, PlatformMetricsSnapshot? pm) in snapshot.Platforms)
        {
            lines.Add($"ghost_scrape_failures{{platform=\"{platform}\"}} {pm.ScrapeFailures}");
        }

        lines.Add("# HELP ghost_cache_hits Total number of cache hits");
        lines.Add("# TYPE ghost_cache_hits counter");
        foreach ((string? platform, PlatformMetricsSnapshot? pm) in snapshot.Platforms)
        {
            lines.Add($"ghost_cache_hits{{platform=\"{platform}\"}} {pm.CacheHits}");
        }

        lines.Add("# HELP ghost_cache_misses Total number of cache misses");
        lines.Add("# TYPE ghost_cache_misses counter");
        foreach ((string? platform, PlatformMetricsSnapshot? pm) in snapshot.Platforms)
        {
            lines.Add($"ghost_cache_misses{{platform=\"{platform}\"}} {pm.CacheMisses}");
        }

        lines.Add("# HELP ghost_circuit_breaker_state Circuit breaker state (0=Closed, 1=HalfOpen, 2=Open)");
        lines.Add("# TYPE ghost_circuit_breaker_state gauge");
        foreach ((string? platform, PlatformMetricsSnapshot? pm) in snapshot.Platforms)
        {
            int stateValue = pm.CircuitBreakerState switch
            {
                "Closed" => 0,
                "HalfOpen" => 1,
                "Open" => 2,
                _ => -1
            };
            lines.Add($"ghost_circuit_breaker_state{{platform=\"{platform}\"}} {stateValue}");
        }

        return Results.Text(string.Join("\n", lines), "text/plain");
    }
}
