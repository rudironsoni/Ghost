using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Kernel.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ghost.WebApi.Features.Jobs;

public static class JobsEndpoints
{
    public static void MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/jobs");
        group.MapPost("/search", SearchJobsAsync);
        group.MapPost("/search-with-errors", SearchJobsWithErrorsAsync);
    }

    private static async Task<IResult> SearchJobsAsync(JobSearchCriteria criteria, [FromServices] IJobClient client, [FromServices] ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        string status = "SUCCESS";
        Exception? caughtEx = null;
        try
        {
            IReadOnlyList<JobListing> result = await client.SearchJobsAsync(criteria, ct).ConfigureAwait(false);
            var response = new
            {
                jobs = result,
                success = true,
                platformErrors = Array.Empty<object>(),
                metadata = new
                {
                    totalPlatforms = 3,
                    successfulPlatforms = 3,
                    failedPlatforms = 0,
                    executionTimeMs = sw.ElapsedMilliseconds
                }
            };
            return Results.Ok(response);
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
                ILogger? logger = loggerFactory?.CreateLogger("JobsEndpoints");
                string platform = client?.PlatformName ?? "Unknown";
                long timeMs = sw.ElapsedMilliseconds;
                string query = criteria?.Query ?? string.Empty;
                // Use LoggerMessage-style delegate to satisfy CA1848/CA2254 and avoid dynamic templates
                Action<ILogger, string, string, long, string, Exception?> jobsLog = LoggerMessage.Define<string, string, long, string>(
                    LogLevel.Information,
                    new EventId(1, nameof(SearchJobsAsync)),
                    "Platform={Platform} Status={Status} TimeMs={TimeMs} Query={Query}");

                // Define an exception logger delegate to avoid CA1848 when logging exceptions
                Action<ILogger, string, Exception?> exceptionLog = LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, nameof(SearchJobsAsync)), "Exception: {Message}");

                if (caughtEx != null)
                {
                    jobsLog(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, platform, status, timeMs, query, caughtEx);
                    exceptionLog(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, caughtEx.Message, caughtEx);
                }
                else
                {
                    jobsLog(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, platform, status, timeMs, query, null);
                }
            }
            catch { /* swallow logging errors to avoid interfering with response */ }
        }
    }

    private static async Task<IResult> SearchJobsWithErrorsAsync(JobSearchCriteria criteria, [FromServices] IJobClient client, CancellationToken ct)
    {
        // Check if the client supports structured error reporting
        if (client is AggregatedJobClient aggregatedClient)
        {
            JobSearchResult result = await aggregatedClient.SearchJobsWithErrorsAsync(criteria, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }

        // Fallback to regular search if structured reporting not supported
        IReadOnlyList<JobListing> jobs = await client.SearchJobsAsync(criteria, ct).ConfigureAwait(false);
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
