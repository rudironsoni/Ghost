using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;

namespace Ghost.Kernel.Services;

public partial class AggregatedJobClient : Ghost.Contracts.Jobs.IJobClient
{
    private readonly List<IJobScraper> _scrapers;
    private readonly IDeduplicationService _dedupe;
    private readonly ILogger<AggregatedJobClient> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ErrorCategorizationService _errorCategorizationService;

    // LoggerMessage source generators (EventIds 2000-2099 for JobClient)
    [LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "AggregatedJobClient constructed with {Count} scrapers: {Names}")]
    private static partial void LogConstructed(ILogger<AggregatedJobClient> logger, int count, string names);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Scraper {Platform} failed")]
    private static partial void LogScraperFailed(ILogger<AggregatedJobClient> logger, string platform, Exception ex);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Injected scrapers count: {Count}")]
    private static partial void LogScraperCount(ILogger<AggregatedJobClient> logger, int count);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Available scraper: '{Name}' (Type: {Type})")]
    private static partial void LogAvailableScraper(ILogger<AggregatedJobClient> logger, string name, string type);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Search criteria sources: {Sources}")]
    private static partial void LogSearchSources(ILogger<AggregatedJobClient> logger, string sources);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Requested sources (normalized): {Sources}")]
    private static partial void LogNormalizedSources(ILogger<AggregatedJobClient> logger, string sources);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "Selected scrapers: {Scrapers}")]
    private static partial void LogSelectedScrapers(ILogger<AggregatedJobClient> logger, string scrapers);

    public AggregatedJobClient(IEnumerable<IJobScraper> scrapers, IDeduplicationService dedupe, ILogger<AggregatedJobClient> logger, TimeProvider? timeProvider = null, ErrorCategorizationService? errorCategorizationService = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        // materialize the incoming enumerable so we can inspect it reliably at runtime
        _scrapers = (scrapers ?? Enumerable.Empty<IJobScraper>()).ToList();
        _dedupe = dedupe;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _errorCategorizationService = errorCategorizationService ?? new ErrorCategorizationService(_timeProvider);

        // log exactly what was injected so we can diagnose missing scrapers
        try
        {
            LogConstructed(_logger, _scrapers.Count, string.Join(", ", _scrapers.Select(s => s.GetType().Name)));
        }
        catch (Exception ex)
        {
            // Logging failed - cannot log the failure, but don't crash
            System.Diagnostics.Debug.WriteLine($"Logging error during construction: {ex.Message}");
        }
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
        DateTime startTime = _timeProvider.GetUtcNow().UtcDateTime;
        JobSearchCriteria criteriaNonNull = criteria ?? new JobSearchCriteria();

        // Log how many scrapers were injected
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogScraperCount(_logger, _scrapers?.Count ?? 0);
                foreach (IJobScraper s in _scrapers ?? Enumerable.Empty<IJobScraper>())
                {
                    LogAvailableScraper(_logger, s.PlatformName ?? "Unknown", s.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }

        // determine which scrapers to run based on criteria.Sources
        IEnumerable<IJobScraper> scrapersToRun = Enumerable.Empty<IJobScraper>();
        if (criteriaNonNull.Sources != null && criteriaNonNull.Sources.Count > 0)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    string sources = string.Join(", ", criteriaNonNull.Sources);
                    LogSearchSources(_logger, sources);
                }
            }
            catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }
            var lower = new HashSet<string>((criteriaNonNull.Sources ?? new List<string>()).Select(s => s?.ToLowerInvariant() ?? string.Empty));
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    string normalizedSources = string.Join(", ", lower);
                    LogNormalizedSources(_logger, normalizedSources);
                }
            }
            catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }
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
                string selectedScrapers = string.Join(", ", (scrapersToRun ?? Enumerable.Empty<IJobScraper>()).Select(s => s.PlatformName ?? "Unknown"));
                LogSelectedScrapers(_logger, selectedScrapers);
            }
        }
        catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }

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
                PlatformError error = _errorCategorizationService.CategorizeError(ex, s.PlatformName ?? "Unknown");
                platformErrors.Add(error);
                LogScraperFailed(_logger, s.PlatformName ?? "Unknown", ex);
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

        TimeSpan executionTime = _timeProvider.GetUtcNow().UtcDateTime - startTime;
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
                LogScraperCount(_logger, _scrapers?.Count ?? 0);
                // log each injected scraper name and type for debugging
                foreach (IJobScraper s in _scrapers ?? Enumerable.Empty<IJobScraper>())
                {
                    LogAvailableScraper(_logger, s.PlatformName ?? "Unknown", s.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }

        // determine which scrapers to run based on criteria.Sources
        IEnumerable<IJobScraper> scrapersToRun = Enumerable.Empty<IJobScraper>();
        if (criteriaNonNull.Sources != null && criteriaNonNull.Sources.Count > 0)
        {
            // log provided sources
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    string sources = string.Join(", ", criteriaNonNull.Sources);
                    LogSearchSources(_logger, sources);
                }
            }
            catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }
            var lower = new HashSet<string>((criteriaNonNull.Sources ?? new List<string>()).Select(s => s?.ToLowerInvariant() ?? string.Empty));
            // log normalized requested sources for debugging
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    string normalizedSources = string.Join(", ", lower);
                    LogNormalizedSources(_logger, normalizedSources);
                }
            }
            catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }
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
                string selectedScrapers = string.Join(", ", (scrapersToRun ?? Enumerable.Empty<IJobScraper>()).Select(s => s.PlatformName ?? "Unknown"));
                LogSelectedScrapers(_logger, selectedScrapers);
            }
        }
        catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }

        Task<IReadOnlyList<JobListing>>[] tasks = (scrapersToRun ?? Enumerable.Empty<IJobScraper>()).Select(s => Task.Run(async () =>
        {
            try
            {
                return await s.SearchJobsAsync(criteriaNonNull, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogScraperFailed(_logger, s.PlatformName ?? "Unknown", ex);
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
                catch (Exception ex)
        {
            // Logging should never fail the operation
            System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
        }
            }
            return new JobListing { Id = jobId };
        }, ct);
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobApplication>)new List<JobApplication>());
    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobListing>)new List<JobListing>());
}
