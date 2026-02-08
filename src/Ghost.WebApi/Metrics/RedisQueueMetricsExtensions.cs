using Ghost.WebApi.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ghost.WebApi.Metrics;

/// <summary>
/// Extension methods for Redis queue metrics service and endpoints.
/// </summary>
public static class RedisQueueMetricsExtensions
{
    /// <summary>
    /// Adds the Redis queue metrics background service to the service collection.
    /// </summary>
    public static IServiceCollection AddRedisQueueMetrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        // Register as singleton first so it can be injected into the endpoint
        services.AddSingleton<RedisQueueMetricsService>();
        
        // Then register as hosted service using the same singleton instance
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RedisQueueMetricsService>());
        
        return services;
    }

    /// <summary>
    /// Maps the Redis queue metrics endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapRedisQueueMetricsEndpoint(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/metrics/redis-queue", GetRedisQueueMetrics)
            .WithName("GetRedisQueueMetrics")
            .WithTags("Metrics")
            .WithSummary("Get Redis queue depth metrics in Prometheus format")
            .AllowAnonymous();

        return app;
    }

    private static IResult GetRedisQueueMetrics(
        [FromServices] RedisQueueMetricsService metricsService)
    {
        var lines = new List<string>
        {
            "# HELP ghost_redis_queue_length Redis queue depth by priority",
            "# TYPE ghost_redis_queue_length gauge",
            $"ghost_redis_queue_length{{queue_name=\"pending\",priority=\"all\"}} {metricsService.PendingCount}",
            "",
            "# HELP ghost_redis_active_jobs Active jobs being processed",
            "# TYPE ghost_redis_active_jobs gauge",
            $"ghost_redis_active_jobs{{queue_name=\"active\",priority=\"all\"}} {metricsService.ActiveCount}",
            "",
            "# HELP ghost_redis_completed_jobs Completed jobs in history",
            "# TYPE ghost_redis_completed_jobs gauge",
            $"ghost_redis_completed_jobs{{queue_name=\"completed\",priority=\"all\"}} {metricsService.CompletedCount}",
            "",
            "# HELP ghost_redis_dead_jobs Dead letter queue depth",
            "# TYPE ghost_redis_dead_jobs gauge",
            $"ghost_redis_dead_jobs{{queue_name=\"dead\",priority=\"all\"}} {metricsService.DeadCount}",
            "",
            "# HELP ghost_redis_metrics_last_update_timestamp Unix timestamp of last metrics update",
            "# TYPE ghost_redis_metrics_last_update_timestamp gauge",
            $"ghost_redis_metrics_last_update_timestamp {new DateTimeOffset(metricsService.LastUpdate).ToUnixTimeSeconds()}"
        };

        return Results.Text(string.Join("\n", lines) + "\n", "text/plain; version=0.0.4");
    }
}
