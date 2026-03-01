using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.Cloud.Api.Endpoints;

public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder api = app.MapGroup("/v1/runs")
            .WithTags("Scrape Runs")
            .WithOpenApi();

        api.MapGet("/{runId}", async (
            string runId,
            [FromServices] IClusterClient clusterClient,
            CancellationToken ct) =>
        {
            IScrapeRunGrain grain = clusterClient.GetGrain<IScrapeRunGrain>(runId);
            ScrapeRunStatus status = await grain.GetStatusAsync().ConfigureAwait(false);
            return Results.Ok(status);
        });

        api.MapGet("/{runId}/results", async (
            string runId,
            [FromServices] IScrapeRunQueries queries,
            [FromQuery] string? cursor,
            [FromQuery] int pageSize = 100,
            CancellationToken ct = default) =>
        {
            IReadOnlyList<ScrapeResultReadModel> results = await queries.GetResultsAsync(runId, cursor, pageSize, ct).ConfigureAwait(false);

            string? nextCursor = results.Count == pageSize && results.Count > 0
                ? results[^1].Id.ToString()
                : null;

            var response = new ScrapeRunResult<JsonElement>
            {
                RunId = runId,
                Items = results.Select(r => r.Data).ToList(),
                NextCursor = nextCursor,
                HasMore = nextCursor != null
            };

            return Results.Ok(response);
        });

        api.MapGet("/{runId}/artifacts", async (
            string runId,
            [FromServices] IArtifactQueries queries,
            CancellationToken ct) =>
        {
            IReadOnlyList<ArtifactMetadataReadModel> artifacts = await queries.GetArtifactsAsync(runId, ct).ConfigureAwait(false);
            return Results.Ok(artifacts);
        });

        return app;
    }
}
