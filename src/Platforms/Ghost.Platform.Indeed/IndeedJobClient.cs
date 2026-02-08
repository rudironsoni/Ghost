using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed.Internal;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.Indeed;

public class IndeedJobClient : Ghost.Abstractions.IJobScraper
{
    private readonly IndeedApiClient _api;
    private readonly ILogger<IndeedJobClient> _logger;
    private static readonly Action<ILogger, int, Exception?> LogRawCount =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1001, "IndeedRawCount"), "IndeedApiClient returned {Count} raw items.");
    private static readonly Action<ILogger, int, Exception?> LogParsedCount =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1002, "ParsedJobListings"), "Parsed {Count} JobListings.");

    public IndeedJobClient(IndeedApiClient api, ILogger<IndeedJobClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    public string PlatformName => "Indeed";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var list = new List<JobListing>();
        int rawCount = 0;
        int parsedCount = 0;
        await foreach (var root in _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, criteria.MaxResults))
        {
            // count raw items (results array length) if present
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("jobSearch", out var jobSearch) && jobSearch.TryGetProperty("results", out var results))
            {
                rawCount += results.GetArrayLength();
            }

            var parsed = IndeedJobParser.ParseJobs(root).ToList();
            parsedCount += parsed.Count;
            list.AddRange(parsed);
        }

        LogRawCount(_logger, rawCount, null);
        LogParsedCount(_logger, parsedCount, null);

        return list;
    }

    /// <summary>
    /// Searches jobs in parallel with bounded concurrency (max 5 requests).
    /// </summary>
    public async IAsyncEnumerable<JobListing> SearchJobsParallelAsync(
        JobSearchCriteria criteria,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var query = criteria.Query ?? string.Empty;
        var location = criteria.Location ?? string.Empty;
        var limit = criteria.MaxResults > 0 ? criteria.MaxResults : 25;

        var firstPage = await FetchPageAsync(query, location, limit, null, null, ct).ConfigureAwait(false);
        foreach (var job in firstPage.Jobs)
        {
            yield return job;
        }

        if (!firstPage.HasNext)
        {
            yield break;
        }

        var semaphore = new SemaphoreSlim(5, 5);
        var tasks = new List<Task<PageResult>>();
        var nextCursor = firstPage.NextCursor;
        var hasNext = firstPage.HasNext;

        while (hasNext || tasks.Count > 0)
        {
            while (hasNext && tasks.Count < 5)
            {
                var cursor = nextCursor;
                tasks.Add(FetchPageAsync(query, location, limit, cursor, semaphore, ct));
                hasNext = false;
                nextCursor = null;
            }

            var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
            tasks.Remove(completed);
            var page = await completed.ConfigureAwait(false);
            foreach (var job in page.Jobs)
            {
                yield return job;
            }

            if (page.HasNext && !string.IsNullOrWhiteSpace(page.NextCursor))
            {
                hasNext = true;
                nextCursor = page.NextCursor;
            }
        }
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(new JobListing { Id = jobId, Source = "Indeed" });

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) =>
        Task.FromResult(new JobApplication());

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<JobApplication>)new List<JobApplication>());

    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<JobListing>)new List<JobListing>());

    private async Task<PageResult> FetchPageAsync(string query, string location, int limit, string? cursor, SemaphoreSlim? semaphore = null, CancellationToken ct = default)
    {
        if (semaphore is not null)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
        }

        try
        {
            var root = await _api.SearchPageAsync(query, location, limit, cursor, ct).ConfigureAwait(false);
            var parsed = IndeedJobParser.ParseJobs(root).ToList();
            var pageInfo = TryGetPageInfo(root);
            var hasNext = pageInfo.HasNext && !string.IsNullOrWhiteSpace(pageInfo.NextCursor);
            return new PageResult(parsed, pageInfo.NextCursor, hasNext);
        }
        finally
        {
            semaphore?.Release();
        }
    }

    private static PageInfo TryGetPageInfo(System.Text.Json.JsonElement root)
    {
        if (root.TryGetProperty("data", out var data)
            && data.TryGetProperty("jobSearch", out var jobSearch)
            && jobSearch.TryGetProperty("pageInfo", out var pageInfo))
        {
            var nextCursor = pageInfo.TryGetProperty("nextCursor", out var nextCursorEl) ? nextCursorEl.GetString() : null;
            var hasNext = pageInfo.TryGetProperty("hasNextPage", out var hasNextEl) && hasNextEl.ValueKind == System.Text.Json.JsonValueKind.True;
            return new PageInfo(nextCursor, hasNext);
        }

        return new PageInfo(null, false);
    }

    private sealed record PageResult(IReadOnlyList<JobListing> Jobs, string? NextCursor, bool HasNext);
    private sealed record PageInfo(string? NextCursor, bool HasNext);
}
