using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;

namespace Ghost.Kernel.Services;

#pragma warning disable CA1848 // Use LoggerMessage delegates for high-performance logging

public class AggregatedJobClient : Ghost.Contracts.Jobs.IJobClient
{
    private readonly List<IJobScraper> _scrapers;
    private readonly IDeduplicationService _dedupe;
    private readonly ILogger<AggregatedJobClient> _logger;
    private static readonly Action<ILogger, string, Exception?> s_logScraperFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(AggregatedJobClient)), "Scraper {Platform} failed");

    public AggregatedJobClient(IEnumerable<IJobScraper> scrapers, IDeduplicationService dedupe, ILogger<AggregatedJobClient> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        // materialize the incoming enumerable so we can inspect it reliably at runtime
        _scrapers = (scrapers ?? Enumerable.Empty<IJobScraper>()).ToList();
        _dedupe = dedupe;

        // log exactly what was injected so we can diagnose missing scrapers
        try
        {
            _logger.LogWarning("AggregatedJobClient constructed with {Count} scrapers: {Names}", _scrapers.Count, string.Join(", ", _scrapers.Select(s => s.GetType().Name)));
        }
        catch { /* swallow logging errors */ }
    }

    public string PlatformName => "Aggregated";

    /// <summary>
    /// Searches for jobs with structured error reporting.
    /// </summary>
    /// <param name="criteria">Search criteria</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Job search result with jobs and error information</returns>
    public async Task<JobSearchResult> SearchJobsWithErrorsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        DateTime startTime = DateTime.UtcNow;
        JobSearchCriteria criteriaNonNull = criteria ?? new JobSearchCriteria();

        // Log how many scrapers were injected
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Injected scrapers count: {Count}", _scrapers?.Count ?? 0);
                foreach (IJobScraper s in _scrapers ?? Enumerable.Empty<IJobScraper>())
                {
                    _logger.LogInformation("Available scraper: '{Name}' (Type: {Type})", s.PlatformName, s.GetType().Name);
                }
            }
        }
        catch { /* swallow any logging errors */ }

        // determine which scrapers to run based on criteria.Sources
        IEnumerable<IJobScraper> scrapersToRun = Enumerable.Empty<IJobScraper>();
        if (criteriaNonNull.Sources != null && criteriaNonNull.Sources.Count > 0)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Search criteria sources: {Sources}", string.Join(", ", criteriaNonNull.Sources));
                }
            }
            catch { }
            var lower = new HashSet<string>((criteriaNonNull.Sources ?? new List<string>()).Select(s => s?.ToLowerInvariant() ?? string.Empty));
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Requested sources (normalized): {Sources}", string.Join(", ", lower));
                }
            }
            catch { }
            scrapersToRun = (_scrapers ?? Enumerable.Empty<IJobScraper>())
                .Where(s => lower.Contains(s.PlatformName?.ToLowerInvariant() ?? string.Empty));
        }
        else
        {
            scrapersToRun = _scrapers ?? Enumerable.Empty<IJobScraper>();
        }

        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Selected scrapers: {Scrapers}", string.Join(", ", (scrapersToRun ?? Enumerable.Empty<IJobScraper>()).Select(s => s.PlatformName)));
            }
        }
        catch { }

        var platformErrors = new ConcurrentBag<PlatformError>();
        int successfulPlatforms = 0;
        int totalPlatforms = scrapersToRun?.Count() ?? 0;

        Task<IReadOnlyList<JobListing>>[] tasks = (scrapersToRun ?? Enumerable.Empty<IJobScraper>()).Select(s => Task.Run(async () =>
        {
            try
            {
                IReadOnlyList<JobListing> result = await s.SearchJobsAsync(criteriaNonNull, ct).ConfigureAwait(false);
                Interlocked.Increment(ref successfulPlatforms);
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                PlatformError error = ErrorCategorizationService.CategorizeError(ex, s.PlatformName ?? "Unknown");
                platformErrors.Add(error);
                s_logScraperFailed(_logger, s.PlatformName ?? "Unknown", ex);
                return (IReadOnlyList<JobListing>)new List<JobListing>();
            }
        }, ct)).ToArray();

        IReadOnlyList<JobListing>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var all = results.SelectMany(r => r ?? new List<JobListing>()).ToList();

        // dedupe by generated id
        Dictionary<string, JobListing> map = [];
        foreach (JobListing? job in all)
        {
            string id = _dedupe.GenerateId(job.Title ?? string.Empty, job.Company ?? string.Empty);
            if (!map.ContainsKey(id)) map[id] = job;
        }

        TimeSpan executionTime = DateTime.UtcNow - startTime;
        var platformErrorsList = platformErrors.ToList();
        bool success = platformErrorsList.Count < totalPlatforms || all.Count > 0;

        return new JobSearchResult
        {
            Jobs = map.Values.ToList(),
            Success = success,
            PlatformErrors = platformErrorsList,
            ErrorMessage = !success ? "All platforms failed to return results" : null,
            Metadata = new SearchMetadata
            {
                TotalPlatforms = totalPlatforms,
                SuccessfulPlatforms = successfulPlatforms,
                FailedPlatforms = platformErrorsList.Count,
                ExecutionTimeMs = (long)executionTime.TotalMilliseconds,
                Criteria = criteriaNonNull
            }
        };
    }

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        // ensure we have a non-null criteria to pass to scrapers
        JobSearchCriteria criteriaNonNull = criteria ?? new JobSearchCriteria();

        // log how many scrapers were injected
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Injected scrapers count: {Count}", _scrapers?.Count ?? 0);
                // log each injected scraper name and type for debugging
                foreach (IJobScraper s in _scrapers ?? Enumerable.Empty<IJobScraper>())
                {
                    _logger.LogInformation("Available scraper: '{Name}' (Type: {Type})", s.PlatformName, s.GetType().Name);
                }
            }
        }
        catch { /* swallow any logging errors */ }

        // determine which scrapers to run based on criteria.Sources
        IEnumerable<IJobScraper> scrapersToRun = Enumerable.Empty<IJobScraper>();
        if (criteriaNonNull.Sources != null && criteriaNonNull.Sources.Count > 0)
        {
            // log provided sources
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Search criteria sources: {Sources}", string.Join(", ", criteriaNonNull.Sources));
                }
            }
            catch { }
            var lower = new HashSet<string>((criteriaNonNull.Sources ?? new List<string>()).Select(s => s?.ToLowerInvariant() ?? string.Empty));
            // log normalized requested sources for debugging
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Requested sources (normalized): {Sources}", string.Join(", ", lower));
                }
            }
            catch { }
            scrapersToRun = (_scrapers ?? Enumerable.Empty<IJobScraper>())
                .Where(s => lower.Contains(s.PlatformName?.ToLowerInvariant() ?? string.Empty));
        }
        else
        {
            scrapersToRun = _scrapers ?? Enumerable.Empty<IJobScraper>();
        }

        // log selected scrapers after filtering
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Selected scrapers: {Scrapers}", string.Join(", ", (scrapersToRun ?? Enumerable.Empty<IJobScraper>()).Select(s => s.PlatformName)));
            }
        }
        catch { }

        Task<IReadOnlyList<JobListing>>[] tasks = (scrapersToRun ?? Enumerable.Empty<IJobScraper>()).Select(s => Task.Run(async () =>
        {
            try
            {
                return await s.SearchJobsAsync(criteriaNonNull, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (_logger != null) s_logScraperFailed(_logger, s.PlatformName, ex);
                return (IReadOnlyList<JobListing>)new List<JobListing>();
            }
        }, ct)).ToArray();

        IReadOnlyList<JobListing>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var all = results.SelectMany(r => r ?? new List<JobListing>()).ToList();

        // dedupe by generated id
        Dictionary<string, JobListing> map = [];
        foreach (JobListing? job in all)
        {
            string id = _dedupe.GenerateId(job.Title ?? string.Empty, job.Company ?? string.Empty);
            if (!map.ContainsKey(id)) map[id] = job;
        }

        return map.Values.ToList();
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        // naive: query scrapers sequentially until one returns details
        return Task.Run(async () =>
        {
            foreach (IJobScraper s in _scrapers)
            {
                try
                {
                    JobListing details = await s.GetJobDetailsAsync(jobId, ct).ConfigureAwait(false);
                    if (details != null && !string.IsNullOrEmpty(details.Title)) return details;
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
            return new JobListing { Id = jobId };
        }, ct);
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobApplication>)new List<JobApplication>());
    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobListing>)new List<JobListing>());
}
