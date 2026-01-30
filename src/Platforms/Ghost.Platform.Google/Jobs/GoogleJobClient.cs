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

        var results = await _api.SearchAsync(
            criteria.Query ?? string.Empty,
            criteria.Location ?? string.Empty);

        if (results.Count > 0)
        {
            return results;
        }

        if (_options.UseBrowserFallback && _browserClient != null)
        {
            s_logUsingBrowserFallback(_logger, criteria.Query ?? "", null);

            try
            {
                var browserResults = await _browserClient.SearchAsync(
                    criteria.Query ?? string.Empty,
                    criteria.Location ?? string.Empty,
                    criteria.MaxResults > 0 ? criteria.MaxResults : 25,
                    ct);

                if (browserResults.Count > 0)
                {
                    s_logBrowserFallbackSuccess(_logger, browserResults.Count, null);
                }

                return browserResults;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                s_logBrowserFallbackFailed(_logger, null);
            }
        }

        return results;
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
