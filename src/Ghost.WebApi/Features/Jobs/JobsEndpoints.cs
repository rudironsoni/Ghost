using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Ghost.WebApi.Features.Jobs;

public static class JobsEndpoints
{
    public static void MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/jobs").MapPost("/search", SearchJobs);
        app.MapGroup("/api/jobs").MapPost("/search-with-errors", SearchJobsWithErrors);
    }

    private static async Task<IResult> SearchJobs([FromBody] JobSearchCriteria criteria, [FromServices] IJobClient client, [FromServices] ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var status = "SUCCESS";
        Exception? caughtEx = null;
        try
        {
            var result = await client.SearchJobsAsync(criteria, ct);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            status = "FAILURE";
            caughtEx = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            try
            {
                var logger = loggerFactory?.CreateLogger("JobsEndpoints");
                var platform = client?.PlatformName ?? "Unknown";
                var timeMs = sw.ElapsedMilliseconds;
                var query = criteria?.Query ?? string.Empty;
                var message = $"[{platform}] [{status}] [{timeMs}] [{query}]";
                if (caughtEx != null)
                {
                    logger?.LogInformation(caughtEx, message + " Exception: {Message}", caughtEx.Message);
                }
                else
                {
                    logger?.LogInformation(message);
                }
            }
            catch { /* swallow logging errors to avoid interfering with response */ }
        }
    }

    private static async Task<IResult> SearchJobsWithErrors([FromBody] JobSearchCriteria criteria, [FromServices] IJobClient client, CancellationToken ct)
    {
        // Check if the client supports structured error reporting
        if (client is AggregatedJobClient aggregatedClient)
        {
            var result = await aggregatedClient.SearchJobsWithErrorsAsync(criteria, ct);
            return Results.Ok(result);
        }

        // Fallback to regular search if structured reporting not supported
        var jobs = await client.SearchJobsAsync(criteria, ct);
        var fallbackResult = new JobSearchResult
        {
            Jobs = jobs,
            Success = true,
            PlatformErrors = new List<PlatformError>(),
            Metadata = new SearchMetadata
            {
                TotalPlatforms = 1,
                SuccessfulPlatforms = 1,
                FailedPlatforms = 0,
                ExecutionTimeMs = 0,
                Criteria = criteria
            }
        };
        return Results.Ok(fallbackResult);
    }
}
