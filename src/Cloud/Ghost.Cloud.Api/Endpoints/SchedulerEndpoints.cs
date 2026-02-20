using Ghost.Cloud.Api.Middleware;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.Cloud.Api.Endpoints;

public static class SchedulerEndpoints
{
    private const string SchedulerGrainKey = "default";

    public static IEndpointRouteBuilder MapSchedulerEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder schedules = app.MapGroup("/v1/schedules")
            .WithTags("Scheduler")
            .WithOpenApi();

        schedules.MapPost("/canary", async (
            [FromBody] CreateCanaryScheduleRequest request,
            HttpContext context,
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }

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
                TenantId = tenantId,
                Input = request.Input,
                ScheduledTime = request.ScheduledTime,
                RequestedMode = "canary",
                RunKind = "canary"
            }).ConfigureAwait(false);

            return Results.Accepted(
                $"/v1/runs/{runId}",
                new
                {
                    RunId = runId,
                    Status = "Pending",
                    request.ScheduledTime,
                    RunKind = "canary"
                });
        });

        schedules.MapGet("/pending", async (
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct) =>
        {
            ISchedulerGrain scheduler = clusterClient.GetGrain<ISchedulerGrain>(SchedulerGrainKey);
            List<ScheduledRunInfo> pendingRuns = await scheduler.GetPendingRunsAsync().ConfigureAwait(false);
            return Results.Ok(pendingRuns);
        });

        schedules.MapPost("/{runId}:cancel", async (
            string runId,
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct) =>
        {
            ISchedulerGrain scheduler = clusterClient.GetGrain<ISchedulerGrain>(SchedulerGrainKey);
            await scheduler.CancelScheduledRunAsync(runId).ConfigureAwait(false);
            return Results.Ok(new { RunId = runId, Status = "Cancelled" });
        });

        return app;
    }

    public sealed record CreateCanaryScheduleRequest
    {
        public string? RunId { get; init; }
        public required string EndpointId { get; init; }
        public DateTimeOffset ScheduledTime { get; init; } = DateTimeOffset.UtcNow;
        public JsonElement Input { get; init; }
    }
}
