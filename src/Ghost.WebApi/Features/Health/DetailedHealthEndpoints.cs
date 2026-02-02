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

        var group = app.MapGroup("/api/health");

        group.MapGet("/detailed", GetDetailedReport);
        group.MapGet("/platforms", GetPlatformHealth);
        group.MapGet("/proxies", GetProxyHealth);
        group.MapGet("/metrics", GetMetrics);

        return app;
    }

    private static async Task<IResult> GetDetailedReport(
        [FromServices] IHealthReportService reportService,
        CancellationToken ct)
    {
        var report = await reportService.BuildReportAsync(ct).ConfigureAwait(false);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetPlatformHealth(
        [FromServices] IHealthReportService reportService,
        CancellationToken ct)
    {
        var report = await reportService.BuildReportAsync(ct).ConfigureAwait(false);
        return Results.Ok(report.Platforms);
    }

    private static async Task<IResult> GetProxyHealth(
        [FromServices] IHealthReportService reportService,
        CancellationToken ct)
    {
        var report = await reportService.BuildReportAsync(ct).ConfigureAwait(false);
        return Results.Ok(report.Proxies);
    }

    private static IResult GetMetrics(
        [FromServices] MetricsService metricsService)
    {
        var snapshot = metricsService.GetSnapshot();
        return Results.Ok(snapshot);
    }
}
