using Ghost.Cloud.Api.Middleware;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.Cloud.Api.Endpoints;

/// <summary>
/// Canary-specific endpoints for outcomes, metrics, and lifecycle operations.
/// </summary>
public static class CanaryEndpoints
{
    public static IEndpointRouteBuilder MapCanaryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder canaries = app.MapGroup("/v1/canaries")
            .WithTags("Canaries")
            .WithOpenApi();

        // CL-005: Get canary outcome by run ID
        canaries.MapGet("/outcomes/{runId}", async (
            string runId,
            HttpContext context,
            [FromServices] ICanaryQueries queries,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }

            CanaryOutcomeReadModel? outcome = await queries.GetCanaryOutcomeAsync(runId, ct).ConfigureAwait(false);

            if (outcome is null)
            {
                return Results.NotFound(new { Error = $"Canary outcome not found for run {runId}." });
            }

            // CL-005: Enforce tenant scoping
            if (outcome.TenantId != tenantId)
            {
                return Results.Forbid();
            }

            return Results.Ok(outcome);
        });

        // CL-005: List canary outcomes for an endpoint
        canaries.MapGet("/outcomes/by-endpoint/{endpointId}", async (
            string endpointId,
            HttpContext context,
            [FromServices] ICanaryQueries queries,
            CancellationToken ct,
            [FromQuery] DateTimeOffset? since = null,
            [FromQuery] int limit = 20) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }
            Guid resolvedTenantId = tenantId ?? throw new InvalidOperationException("Tenant ID resolution failed.");

            DateTimeOffset lookback = since ?? DateTimeOffset.UtcNow.AddDays(-7);

            IReadOnlyList<CanaryOutcomeReadModel> outcomes = await queries
                .GetCanaryOutcomesByEndpointAsync(endpointId, lookback, limit, ct)
                .ConfigureAwait(false);

            // CL-005: Filter by tenant for security
            var tenantOutcomes = outcomes
                .Where(o => o.TenantId == resolvedTenantId)
                .ToList();

            return Results.Ok(tenantOutcomes);
        });

        // CL-005: List canary outcomes for tenant
        canaries.MapGet("/outcomes", async (
            HttpContext context,
            [FromServices] ICanaryQueries queries,
            CancellationToken ct,
            [FromQuery] DateTimeOffset? since = null,
            [FromQuery] int limit = 20) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }
            Guid resolvedTenantId = tenantId ?? throw new InvalidOperationException("Tenant ID resolution failed.");

            DateTimeOffset lookback = since ?? DateTimeOffset.UtcNow.AddDays(-7);

            IReadOnlyList<CanaryOutcomeReadModel> outcomes = await queries
                .GetCanaryOutcomesByTenantAsync(resolvedTenantId, lookback, limit, ct)
                .ConfigureAwait(false);

            return Results.Ok(outcomes);
        });

        // CL-005: Get quality metrics for an endpoint
        canaries.MapGet("/metrics/{endpointId}", async (
            string endpointId,
            HttpContext context,
            [FromServices] ICanaryQueries queries,
            [FromQuery] DateTimeOffset? since,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid? tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }

            DateTimeOffset lookback = since ?? DateTimeOffset.UtcNow.AddDays(-7);

            CanaryQualityMetrics? metrics = await queries
                .GetQualityMetricsAsync(endpointId, lookback, ct)
                .ConfigureAwait(false);

            if (metrics is null)
            {
                return Results.NotFound(new { Error = $"No metrics found for endpoint {endpointId}." });
            }

            // CL-005: Enforce tenant scoping
            if (metrics.TenantId != tenantId)
            {
                return Results.Forbid();
            }

            return Results.Ok(metrics);
        });

        // CL-005: Execute canary on demand
        canaries.MapPost("/execute", async (
            [FromBody] ExecuteCanaryRequest request,
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

            string runId = $"canary-exec-{Guid.NewGuid():N}";

            // Trigger immediate execution via scheduler with immediate schedule time
            ISchedulerGrain scheduler = clusterClient.GetGrain<ISchedulerGrain>("default");
            await scheduler.ScheduleRunAsync(new ScheduledRunRequest
            {
                RunId = runId,
                EndpointId = request.EndpointId,
                TenantId = resolvedTenantId,
                Input = request.Input,
                ScheduledTime = DateTimeOffset.UtcNow.AddSeconds(5), // Slight delay for processing
                RequestedMode = "canary",
                RunKind = "canary",
                CanaryMetadata = new CanaryMetadata
                {
                    ExpectedOutcome = request.ExpectedOutcome,
                    TimeoutSeconds = request.TimeoutSeconds ?? 30,
                    CaptureDiagnostics = request.CaptureDiagnostics ?? true
                }
            }).ConfigureAwait(false);

            return Results.Accepted(
                $"/v1/canaries/outcomes/{runId}",
                new
                {
                    RunId = runId,
                    Status = "Pending",
                    request.EndpointId,
                    Message = "Canary execution scheduled. Check outcome endpoint for results."
                });
        });

        return app;
    }

    // CL-005: Request record for on-demand canary execution
    public sealed record ExecuteCanaryRequest
    {
        public required string EndpointId { get; init; }
        public JsonElement Input { get; init; }
        public string? ExpectedOutcome { get; init; }
        public int? TimeoutSeconds { get; init; }
        public bool? CaptureDiagnostics { get; init; }
    }
}
