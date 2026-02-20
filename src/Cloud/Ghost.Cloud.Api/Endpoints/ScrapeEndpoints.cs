using Ghost.Cloud.Api.Middleware;
using Ghost.Cloud.Contracts.Delivery;
using Ghost.Cloud.Contracts.Endpoints;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.Cloud.Api.Endpoints;

public static class ScrapeEndpoints
{
    public static IEndpointRouteBuilder MapScrapeEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder api = app.MapGroup("/v1/endpoints")
            .WithTags("Scrape Endpoints")
            .WithOpenApi();

        api.MapPost("/{endpointId}:trigger", async (
            string endpointId,
            [FromBody] TriggerScrapeRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromServices] IClusterClient clusterClient,
            [FromServices] IEndpointValidator validator,
            HttpContext context,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }

            IEndpointGrain endpointGrain = clusterClient.GetGrain<IEndpointGrain>(endpointId);

            EndpointManifest manifest;
            try
            {
                manifest = await endpointGrain.GetManifestAsync().ConfigureAwait(false);
            }
            catch
            {
                return Results.NotFound(new { Error = "Endpoint not found" });
            }

            // Validate input against schema
            if (!await validator.ValidateAsync(manifest.InputSchema, request.Input).ConfigureAwait(false))
            {
                return Results.BadRequest(new { Error = "Input validation failed" });
            }

            string runId = GenerateRunId(idempotencyKey);

            IScrapeRunGrain runGrain = clusterClient.GetGrain<IScrapeRunGrain>(runId);
            ScrapeRunStatus status = await runGrain.TriggerAsync(new ScrapeRunRequest
            {
                EndpointId = endpointId,
                Input = request.Input,
                Delivery = request.Delivery,
                IdempotencyKey = idempotencyKey,
                RequestedMode = "async",
                TenantId = tenantId
            }).ConfigureAwait(false);

            if (status.Status == "Failed")
            {
                return Results.BadRequest(new { Error = status.ErrorMessage });
            }

            return Results.Accepted($"/v1/runs/{runId}", new TriggerScrapeResponse
            {
                RunId = runId,
                Status = status.Status,
                ResultSinkUri = BuildResultUri(request.Delivery),
                EstimatedCompletion = DateTimeOffset.UtcNow.AddMinutes(5)
            });
        });

        api.MapPost("/{endpointId}:scrape", async (
            string endpointId,
            [FromBody] TriggerScrapeRequest request,
            [FromServices] IClusterClient clusterClient,
            [FromServices] IEndpointValidator validator,
            HttpContext context,
            CancellationToken ct) =>
        {
            if (!context.TryGetTenantId(out Guid tenantId))
            {
                return Results.BadRequest(new { Error = "Tenant ID is required." });
            }

            IEndpointGrain endpointGrain = clusterClient.GetGrain<IEndpointGrain>(endpointId);

            EndpointManifest manifest;
            try
            {
                manifest = await endpointGrain.GetManifestAsync().ConfigureAwait(false);
            }
            catch
            {
                return Results.NotFound(new { Error = "Endpoint not found" });
            }

            // Validate input against schema
            if (!await validator.ValidateAsync(manifest.InputSchema, request.Input).ConfigureAwait(false))
            {
                return Results.BadRequest(new { Error = "Input validation failed" });
            }

            string runId = GenerateRunId(null);
            IScrapeRunGrain runGrain = clusterClient.GetGrain<IScrapeRunGrain>(runId);

            ScrapeRunStatus status = await runGrain.TriggerAsync(new ScrapeRunRequest
            {
                EndpointId = endpointId,
                Input = request.Input,
                RequestedMode = "sync",
                TenantId = tenantId
            }).ConfigureAwait(false);

            if (status.Status == "Failed")
            {
                return Results.BadRequest(new { Error = status.ErrorMessage });
            }

            return Results.Accepted($"/v1/runs/{runId}", new TriggerScrapeResponse
            {
                RunId = runId,
                Status = status.Status
            });
        });

        return app;
    }

    private static string GenerateRunId(string? idempotencyKey) =>
        idempotencyKey != null
            ? Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyKey)))[..22]
            : Guid.NewGuid().ToString("N");

    private static string? BuildResultUri(DeliveryConfig? config) =>
        config?.ResultSink?.Type switch
        {
            "s3" => $"s3://{config.ResultSink.Uri}",
            "gcs" => $"gs://{config.ResultSink.Uri}",
            "azure" => $"azure://{config.ResultSink.Uri}",
            _ => null
        };
}
