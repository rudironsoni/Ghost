using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Plugin.Indeed.Internal;
using Ghost.Plugin.Indeed.Jobs;
using Microsoft.Extensions.Logging;
using LoggerMessage = Microsoft.Extensions.Logging.LoggerMessage;

namespace Ghost.Plugin.Indeed;

public class IndeedJobClient : Ghost.Abstractions.IJobScraper
{
    private readonly IndeedApiClient _api;
    private readonly IndeedSearchScraper? _searchScraper;
    private readonly IndeedJobDetailsScraper? _detailsScraper;
    private readonly ILogger<IndeedJobClient> _logger;
    private static readonly Action<ILogger, int, Exception?> LogRawCount =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1001, "IndeedRawCount"), "IndeedApiClient returned {Count} raw items.");
    private static readonly Action<ILogger, int, Exception?> LogParsedCount =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1002, "ParsedJobListings"), "Parsed {Count} JobListings.");

    /// <summary>
    /// Legacy constructor for backward compatibility (API-only).
    /// </summary>
    public IndeedJobClient(IndeedApiClient api, ILogger<IndeedJobClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// Modern constructor with multi-strategy scraper support.
    /// </summary>
    public IndeedJobClient(
        IndeedApiClient api,
        ILogger<IndeedJobClient> logger,
        IndeedSearchScraper? searchScraper = null,
        IndeedJobDetailsScraper? detailsScraper = null)
    {
        _api = api;
        _logger = logger;
        _searchScraper = searchScraper;
        _detailsScraper = detailsScraper;
    }

    public string PlatformName => "Indeed";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        // Use multi-strategy scraper if available (API + browser fallback)
        if (_searchScraper != null)
        {
            IReadOnlyList<JobListing> results = await _searchScraper.SearchAsync(criteria, ct).ConfigureAwait(false);
            LogParsedCount(_logger, results.Count, null);
            return results;
        }

        // Fallback to legacy API-only approach
        var list = new List<JobListing>();
        int rawCount = 0;
        int parsedCount = 0;
        await foreach (JsonElement root in _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, criteria.MaxResults).ConfigureAwait(false))
        {
            // count raw items (results array length) if present
            if (root.TryGetProperty("data", out JsonElement data) && data.TryGetProperty("jobSearch", out JsonElement jobSearch) && jobSearch.TryGetProperty("results", out JsonElement results))
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
        string query = criteria.Query ?? string.Empty;
        string location = criteria.Location ?? string.Empty;
        int limit = criteria.MaxResults > 0 ? criteria.MaxResults : 25;

        PageResult firstPage = await FetchPageAsync(query, location, limit, null, null, ct).ConfigureAwait(false);
        foreach (JobListing job in firstPage.Jobs)
        {
            yield return job;
        }

        if (!firstPage.HasNext)
        {
            yield break;
        }

        var semaphore = new SemaphoreSlim(5, 5);
        var tasks = new List<Task<PageResult>>();
        string? nextCursor = firstPage.NextCursor;
        bool hasNext = firstPage.HasNext;

        while (hasNext || tasks.Count > 0)
        {
            while (hasNext && tasks.Count < 5)
            {
                string? cursor = nextCursor;
                tasks.Add(FetchPageAsync(query, location, limit, cursor, semaphore, ct));
                hasNext = false;
                nextCursor = null;
            }

            Task<PageResult> completed = await Task.WhenAny(tasks).ConfigureAwait(false);
            tasks.Remove(completed);
            PageResult page = await completed.ConfigureAwait(false);
            foreach (JobListing job in page.Jobs)
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

    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        // Use details scraper if available
        if (_detailsScraper != null)
        {
            return await _detailsScraper.GetDetailsAsync(jobId, ct).ConfigureAwait(false);
        }

        // Fallback to stub implementation
        return new JobListing { Id = jobId, Source = "Indeed" };
    }

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
            JsonElement root = await _api.SearchPageAsync(query, location, limit, cursor, ct).ConfigureAwait(false);
            var parsed = IndeedJobParser.ParseJobs(root).ToList();
            PageInfo pageInfo = TryGetPageInfo(root);
            bool hasNext = pageInfo.HasNext && !string.IsNullOrWhiteSpace(pageInfo.NextCursor);
            return new PageResult(parsed, pageInfo.NextCursor, hasNext);
        }
        finally
        {
            semaphore?.Release();
        }
    }

    private static PageInfo TryGetPageInfo(System.Text.Json.JsonElement root)
    {
        if (root.TryGetProperty("data", out JsonElement data)
            && data.TryGetProperty("jobSearch", out JsonElement jobSearch)
            && jobSearch.TryGetProperty("pageInfo", out JsonElement pageInfo))
        {
            string? nextCursor = pageInfo.TryGetProperty("nextCursor", out JsonElement nextCursorEl) ? nextCursorEl.GetString() : null;
            bool hasNext = pageInfo.TryGetProperty("hasNextPage", out JsonElement hasNextEl) && hasNextEl.ValueKind == System.Text.Json.JsonValueKind.True;
            return new PageInfo(nextCursor, hasNext);
        }

        return new PageInfo(null, false);
    }

    private sealed record PageResult(IReadOnlyList<JobListing> Jobs, string? NextCursor, bool HasNext);
    private sealed record PageInfo(string? NextCursor, bool HasNext);
}
