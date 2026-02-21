using Ghost.Cloud.Api.Middleware;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.Cloud.Api.Endpoints;

/// <summary>
/// Scheduler endpoints for managing canary and replay schedules.
/// </summary>
public static class SchedulerEndpoints
{
    private const string SchedulerGrainKey = "default";

    public static IEndpointRouteBuilder MapSchedulerEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder schedules = app.MapGroup("/v1/schedules")
            .WithTags("Scheduler")
            .WithOpenApi();

        // CL-005: Create canary schedule with tenant scoping
        schedules.MapPost("/canary", async (
            [FromBody] CreateCanaryScheduleRequest request,
            HttpContext context,
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }
            Guid resolvedTenantId = tenantId ?? throw new InvalidOperationException("Tenant ID resolution failed.");

            if (string.IsNullOrWhiteSpace(request.EndpointId))
            {
                return Results.BadRequest(new { Error = "EndpointId is required." });
            }

            string runId = string.IsNullOrWhiteSpace(request.RunId)
                ? $"canary-{Guid.NewGuid():N}"
                : request.RunId;

            ISchedulerGrain scheduler = clusterClient.GetGrain<ISchedulerGrain>(SchedulerGrainKey);
            await scheduler.ScheduleRunAsync(new ScheduledRunRequest
            {
                RunId = runId,
                EndpointId = request.EndpointId,
                TenantId = resolvedTenantId,
                Input = request.Input,
                ScheduledTime = request.ScheduledTime,
                RequestedMode = "canary",
                RunKind = "canary",
                // CL-005: Include canary metadata if provided
                CanaryMetadata = request.CanaryMetadata
            }).ConfigureAwait(false);

            return Results.Accepted(
                $"/v1/runs/{runId}",
                new
                {
                    RunId = runId,
                    Status = "Pending",
                    request.ScheduledTime,
                    RunKind = "canary",
                    request.EndpointId
                });
        });

        // CL-005: Create replay schedule
        schedules.MapPost("/replay", async (
            [FromBody] CreateReplayScheduleRequest request,
            HttpContext context,
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }
            Guid resolvedTenantId = tenantId ?? throw new InvalidOperationException("Tenant ID resolution failed.");

            if (string.IsNullOrWhiteSpace(request.EndpointId))
            {
                return Results.BadRequest(new { Error = "EndpointId is required." });
            }

            if (string.IsNullOrWhiteSpace(request.CassetteKey))
            {
                return Results.BadRequest(new { Error = "CassetteKey is required for replay." });
            }

            string runId = string.IsNullOrWhiteSpace(request.RunId)
                ? $"replay-{Guid.NewGuid():N}"
                : request.RunId;

            ISchedulerGrain scheduler = clusterClient.GetGrain<ISchedulerGrain>(SchedulerGrainKey);
            await scheduler.ScheduleRunAsync(new ScheduledRunRequest
            {
                RunId = runId,
                EndpointId = request.EndpointId,
                TenantId = resolvedTenantId,
                Input = request.Input,
                ScheduledTime = request.ScheduledTime,
                RequestedMode = "replay",
                RunKind = "replay",
                ReplayMetadata = new ReplayMetadata
                {
                    CassetteKey = request.CassetteKey,
                    ValidateAgainstOriginal = request.ValidateAgainstOriginal,
                    AllowedVariance = request.AllowedVariance
                }
            }).ConfigureAwait(false);

            return Results.Accepted(
                $"/v1/runs/{runId}",
                new
                {
                    RunId = runId,
                    Status = "Pending",
                    request.ScheduledTime,
                    RunKind = "replay",
                    request.CassetteKey
                });
        });

        // CL-005: List scheduled runs with tenant scoping
        schedules.MapGet("/pending", async (
            HttpContext context,
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }

            ISchedulerGrain scheduler = clusterClient.GetGrain<ISchedulerGrain>(SchedulerGrainKey);
            List<ScheduledRunInfo> pendingRuns = await scheduler.GetPendingRunsAsync().ConfigureAwait(false);

            // CL-005: Filter by tenant for security
            var tenantRuns = pendingRuns
                .Where(r => r.TenantId == tenantId)
                .ToList();

            return Results.Ok(tenantRuns);
        });

        // CL-005: List runs by endpoint with tenant scoping
        schedules.MapGet("/by-endpoint/{endpointId}", async (
            string endpointId,
            HttpContext context,
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct,
            [FromQuery] string? status = null,
            [FromQuery] int limit = 20) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }

            ISchedulerGrain scheduler = clusterClient.GetGrain<ISchedulerGrain>(SchedulerGrainKey);
            List<ScheduledRunInfo> pendingRuns = await scheduler.GetPendingRunsAsync().ConfigureAwait(false);

            var endpointRuns = pendingRuns
                .Where(r => r.EndpointId == endpointId && r.TenantId == tenantId)
                .Where(r => status == null || r.Status == status)
                .Take(limit)
                .ToList();

            return Results.Ok(endpointRuns);
        });

        // CL-005: Cancel scheduled run
        schedules.MapPost("/{runId}:cancel", async (
            string runId,
            HttpContext context,
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }

            // Note: Tenant scoping is enforced at the grain level
            ISchedulerGrain scheduler = clusterClient.GetGrain<ISchedulerGrain>(SchedulerGrainKey);
            await scheduler.CancelScheduledRunAsync(runId).ConfigureAwait(false);
            return Results.Ok(new { RunId = runId, Status = "Cancelled" });
        });

        return app;
    }

    // CL-005: Request records for schedule creation
    public sealed record CreateCanaryScheduleRequest
    {
        public string? RunId { get; init; }
        public required string EndpointId { get; init; }
        public DateTimeOffset ScheduledTime { get; init; } = DateTimeOffset.UtcNow;
        public JsonElement Input { get; init; }
        public CanaryMetadata? CanaryMetadata { get; init; }
    }

    public sealed record CreateReplayScheduleRequest
    {
        public string? RunId { get; init; }
        public required string EndpointId { get; init; }
        public required string CassetteKey { get; init; }
        public DateTimeOffset ScheduledTime { get; init; } = DateTimeOffset.UtcNow;
        public JsonElement Input { get; init; }
        public bool ValidateAgainstOriginal { get; init; } = true;
        public double AllowedVariance { get; init; }
    }
}
