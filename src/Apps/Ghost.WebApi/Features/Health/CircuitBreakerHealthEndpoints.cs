using Ghost.Resilience;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.WebApi.Features.Health;

public class CircuitBreakerHealthStatus
{
    public string Platform { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public CircuitBreakerMetrics Metrics { get; set; } = null!;
}

public class CircuitBreakersStatus
{
    public DateTime Timestamp { get; set; }
    public string OverallStatus { get; set; } = "unknown";
    public List<CircuitBreakerHealthStatus> CircuitBreakers { get; set; } = [];
}

public static class CircuitBreakerHealthEndpoints
{
    public static void MapCircuitBreakerHealth(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/health/circuit-breakers")
            .WithTags("Health")
            .WithOpenApi();

        group.MapGet("/", GetAllCircuitBreakers)
            .WithName("GetAllCircuitBreakers")
            .WithSummary("Get health status of all circuit breakers");

        group.MapGet("/{platform}", GetCircuitBreakerByPlatform)
            .WithName("GetCircuitBreakerByPlatform")
            .WithSummary("Get health status of a specific circuit breaker");
    }

    private static IResult GetAllCircuitBreakers(
        [FromServices] IEnumerable<ICircuitBreaker> circuitBreakers,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var status = circuitBreakers
            .Select(cb => new CircuitBreakerHealthStatus
            {
                Platform = cb.Platform,
                State = cb.State.ToString(),
                IsHealthy = cb.State != CircuitState.Open,
                Metrics = cb.GetMetrics()
            })
            .OrderBy(h => h.Platform)
            .ToList();

        return Results.Ok(new CircuitBreakersStatus
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = DetermineOverallStatus(status),
            CircuitBreakers = status
        });
    }

    private static IResult GetCircuitBreakerByPlatform(
        string platform,
        [FromServices] IEnumerable<ICircuitBreaker> circuitBreakers,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        ICircuitBreaker? breaker = circuitBreakers.FirstOrDefault(cb =>
            cb.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));

        if (breaker is null)
        {
            return Results.NotFound(new { error = $"Circuit breaker for platform '{platform}' not found" });
        }

        return Results.Ok(new CircuitBreakerHealthStatus
        {
            Platform = breaker.Platform,
            State = breaker.State.ToString(),
            IsHealthy = breaker.State != CircuitState.Open,
            Metrics = breaker.GetMetrics()
        });
    }

    private static string DetermineOverallStatus(List<CircuitBreakerHealthStatus> statuses)
    {
        if (statuses.Count == 0)
            return "unknown";

        int healthy = statuses.Count(s => s.IsHealthy);
        int open = statuses.Count(s => s.State == "Open");
        int halfOpen = statuses.Count(s => s.State == "HalfOpen");

        if (open > 0)
            return "unhealthy";

        if (halfOpen > 0)
            return "degraded";

        return healthy == statuses.Count ? "healthy" : "partial";
    }
}
