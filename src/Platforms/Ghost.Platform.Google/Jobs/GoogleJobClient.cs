using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Google.Jobs;

public sealed class GoogleJobClient : Ghost.Abstractions.IJobScraper
{
    private readonly Internal.GoogleJobsApiClient _api;
    private readonly Internal.GoogleJobsBrowserClient? _browserClient;
    private readonly ILogger<GoogleJobClient> _logger;
    private readonly GoogleJobsOptions _options;

    private static readonly Action<ILogger, string, Exception?> s_logUsingBrowserFallback =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(SearchJobsAsync)), "HTTP client returned no results for '{Query}', trying browser fallback");

    private static readonly Action<ILogger, int, Exception?> s_logBrowserFallbackSuccess =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(2, nameof(SearchJobsAsync)), "Browser fallback successful, found {Count} jobs");

    private static readonly Action<ILogger, Exception?> s_logBrowserFallbackFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(SearchJobsAsync)), "Browser fallback failed");

    private static readonly Action<ILogger, string, Exception?> s_logStrategyAttempt =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, nameof(SearchJobsAsync)), "Attempting job search with strategy: {Strategy}");

    private static readonly Action<ILogger, string, int, Exception?> s_logStrategySuccess =
        LoggerMessage.Define<string, int>(LogLevel.Information, new EventId(5, nameof(SearchJobsAsync)), "Strategy {Strategy} succeeded, found {Count} jobs");

    private static readonly Action<ILogger, string, Exception?> s_logStrategyFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(6, nameof(SearchJobsAsync)), "Strategy {Strategy} failed, attempting fallback");

    private static readonly Action<ILogger, string, Exception?> s_logStrategyNoFallback =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(7, nameof(SearchJobsAsync)), "Strategy {Strategy} failed, no fallback available");

    public GoogleJobClient(
        Internal.GoogleJobsApiClient api,
        ILogger<GoogleJobClient> logger,
        IOptions<GoogleJobsOptions> options)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleJobClient>.Instance;
        _options = options?.Value ?? new GoogleJobsOptions();
    }

    public GoogleJobClient(
        Internal.GoogleJobsApiClient api,
        Internal.GoogleJobsBrowserClient browserClient,
        ILogger<GoogleJobClient> logger,
        IOptions<GoogleJobsOptions> options)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _browserClient = browserClient ?? throw new ArgumentNullException(nameof(browserClient));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleJobClient>.Instance;
        _options = options?.Value ?? new GoogleJobsOptions();
    }

    public string PlatformName => "Google";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var strategy = _options.Strategy;
        s_logStrategyAttempt(_logger, strategy.ToString(), null);

        try
        {
            return strategy switch
            {
                JobSearchStrategy.BrowserFirst => await TryBrowserFirstAsync(criteria, ct),
                JobSearchStrategy.HttpFirst => await TryHttpFirstAsync(criteria, ct),
                JobSearchStrategy.BrowserOnly => await TryBrowserOnlyAsync(criteria, ct),
                JobSearchStrategy.HttpOnly => await TryHttpOnlyAsync(criteria, ct),
                _ => await TryBrowserFirstAsync(criteria, ct)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private async Task<IReadOnlyList<JobListing>> TryBrowserFirstAsync(JobSearchCriteria criteria, CancellationToken ct)
    {
        if (_browserClient == null)
        {
            s_logStrategyFailed(_logger, "BrowserFirst", null);
            return await TryHttpOnlyAsync(criteria, ct);
        }

        try
        {
            var results = await _browserClient.SearchAsync(
                criteria.Query ?? string.Empty,
                criteria.Location ?? string.Empty,
                criteria.MaxResults > 0 ? criteria.MaxResults : 25,
                ct);

            if (results.Count > 0)
            {
                s_logStrategySuccess(_logger, "BrowserFirst", results.Count, null);
                return results;
            }

            s_logStrategyFailed(_logger, "BrowserFirst", null);
            return await TryHttpOnlyAsync(criteria, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            s_logStrategyFailed(_logger, "BrowserFirst", null);
            return await TryHttpOnlyAsync(criteria, ct);
        }
    }

    private async Task<IReadOnlyList<JobListing>> TryHttpFirstAsync(JobSearchCriteria criteria, CancellationToken ct)
    {
        try
        {
            var results = await _api.SearchAsync(
                criteria.Query ?? string.Empty,
                criteria.Location ?? string.Empty);

            if (results.Count > 0)
            {
                s_logStrategySuccess(_logger, "HttpFirst", results.Count, null);
                return results;
            }

            s_logStrategyFailed(_logger, "HttpFirst", null);
            return await TryBrowserOnlyAsync(criteria, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            s_logStrategyFailed(_logger, "HttpFirst", null);
            return await TryBrowserOnlyAsync(criteria, ct);
        }
    }

    private async Task<IReadOnlyList<JobListing>> TryBrowserOnlyAsync(JobSearchCriteria criteria, CancellationToken ct)
    {
        if (_browserClient == null)
        {
            s_logStrategyNoFallback(_logger, "BrowserOnly", null);
            return Array.Empty<JobListing>();
        }

        try
        {
            var results = await _browserClient.SearchAsync(
                criteria.Query ?? string.Empty,
                criteria.Location ?? string.Empty,
                criteria.MaxResults > 0 ? criteria.MaxResults : 25,
                ct);

            s_logStrategySuccess(_logger, "BrowserOnly", results.Count, null);
            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            s_logStrategyNoFallback(_logger, "BrowserOnly", null);
            return Array.Empty<JobListing>();
        }
    }

    private async Task<IReadOnlyList<JobListing>> TryHttpOnlyAsync(JobSearchCriteria criteria, CancellationToken ct)
    {
        try
        {
            var results = await _api.SearchAsync(
                criteria.Query ?? string.Empty,
                criteria.Location ?? string.Empty);

            s_logStrategySuccess(_logger, "HttpOnly", results.Count, null);
            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            s_logStrategyNoFallback(_logger, "HttpOnly", null);
            return Array.Empty<JobListing>();
        }
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
}
