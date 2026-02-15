using Ghost.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.WebApi.Features.Health;

/// <summary>
/// Detailed health endpoints for monitoring.
/// </summary>
public static class DetailedHealthEndpoints
{
    /// <summary>
    /// Maps detailed health endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapDetailedHealth(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup("/api/health");

        group.MapGet("/detailed", GetDetailedReportAsync);
        group.MapGet("/platforms", GetPlatformHealthAsync);
        group.MapGet("/proxies", GetProxyHealthAsync);
        group.MapGet("/metrics", GetMetrics);

        return app;
    }

    private static async Task<IResult> GetDetailedReportAsync(
        [FromServices] IHealthReportService reportService,
        CancellationToken ct)
    {
        HealthReport report = await reportService.BuildReportAsync(ct).ConfigureAwait(false);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetPlatformHealthAsync(
        [FromServices] IHealthReportService reportService,
        CancellationToken ct)
    {
        HealthReport report = await reportService.BuildReportAsync(ct).ConfigureAwait(false);
        return Results.Ok(report.Platforms);
    }

    private static async Task<IResult> GetProxyHealthAsync(
        [FromServices] IHealthReportService reportService,
        CancellationToken ct)
    {
        HealthReport report = await reportService.BuildReportAsync(ct).ConfigureAwait(false);
        return Results.Ok(report.Proxies);
    }

    private static IResult GetMetrics(
        [FromServices] MetricsService metricsService)
    {
        MetricsSnapshot snapshot = metricsService.GetSnapshot();
        return Results.Ok(snapshot);
    }
}
