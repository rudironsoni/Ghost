using Ghost.Contracts.Jobs;
using Ghost.Platform.Tecnoempleo.Jobs.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Threading;

namespace Ghost.Platform.Tecnoempleo.Jobs;

public class TecnoempleoClient : IJobClient
{
    private readonly TecnoempleoApiClient _apiClient;
    private readonly TecnoempleoOptions _options;
    private readonly ILogger<TecnoempleoClient> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    private static readonly Action<ILogger, int, Exception?> LogRetryWarning =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(1, nameof(LogRetryWarning)), "Retry {RetryCount} for Tecnoempleo API call");

    private static readonly Action<ILogger, int, string, Exception?> LogSearchSuccess =
        LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(2, nameof(LogSearchSuccess)), "Found {JobCount} Tecnoempleo jobs for query: {Query}");

    private static readonly Action<ILogger, string, Exception?> LogSearchError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, nameof(LogSearchError)), "Failed to search Tecnoempleo jobs for query: {Query}");

    private static readonly Action<ILogger, string, Exception?> LogDetailsSuccess =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, nameof(LogDetailsSuccess)), "Retrieved Tecnoempleo job details for ID: {JobId}");

    private static readonly Action<ILogger, string, Exception?> LogDetailsError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(5, nameof(LogDetailsError)), "Failed to get Tecnoempleo job details for ID: {JobId}");

    private static readonly Action<ILogger, string, long, Exception?> LogRequestTiming =
        LoggerMessage.Define<string, long>(LogLevel.Debug, new EventId(6, nameof(LogRequestTiming)), "Tecnoempleo {Operation} completed in {ElapsedMs}ms");

    private static readonly Action<ILogger, string, string, int, Exception?> LogSearchStarted =
        LoggerMessage.Define<string, string, int>(LogLevel.Information, new EventId(7, nameof(LogSearchStarted)), "Starting Tecnoempleo search: Query='{Query}', Location='{Location}', MaxResults={MaxResults}");

    private static readonly Action<ILogger, int, int, Exception?> LogRetryAttempt =
        LoggerMessage.Define<int, int>(LogLevel.Warning, new EventId(8, nameof(LogRetryAttempt)), "Retry attempt {AttemptNumber}/{MaxRetries} for Tecnoempleo API");

    private int _searchRequestCount;
    private int _detailsRequestCount;

    public string PlatformName => "Tecnoempleo";

    public TecnoempleoClient(TecnoempleoApiClient apiClient, IOptions<TecnoempleoOptions> options, ILogger<TecnoempleoClient> logger)
    {
        _apiClient = apiClient;
        _options = options.Value;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _options.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    LogRetryWarning(_logger, retryCount, exception);
                });
    }

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var operation = "SearchJobs";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var query = criteria.Query ?? string.Empty;
                var location = criteria.Location ?? string.Empty;
                var maxResults = criteria.MaxResults;
                var pageSize = Math.Min(maxResults, 20);
                var page = 1;

                LogSearchStarted(_logger, query, location, maxResults, null);
                var jobs = await _apiClient.SearchJobsAsync(query, location, page, pageSize);
                
                Interlocked.Increment(ref _searchRequestCount);
                LogSearchSuccess(_logger, jobs.Count, query, null);
                
                return jobs.Take(maxResults).ToList();
            });
        }
        catch (Exception ex)
        {
            LogSearchError(_logger, criteria.Query ?? string.Empty, ex);
            return new List<JobListing>();
        }
        finally
        {
            sw.Stop();
            LogRequestTiming(_logger, operation, sw.ElapsedMilliseconds, null);
        }
    }

    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        var operation = "GetJobDetails";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var job = await _apiClient.GetJobDetailsAsync(jobId);
                
                Interlocked.Increment(ref _detailsRequestCount);
                LogDetailsSuccess(_logger, jobId, null);
                
                return job;
            });
        }
        catch (Exception ex)
        {
            LogDetailsError(_logger, jobId, ex);
            throw;
        }
        finally
        {
            sw.Stop();
            LogRequestTiming(_logger, operation, sw.ElapsedMilliseconds, null);
        }
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
    {
        throw new NotImplementedException("Job application not implemented for Tecnoempleo");
    }

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("Applications retrieval not implemented for Tecnoempleo");
    }

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
    {
        throw new NotImplementedException("Job saving not implemented for Tecnoempleo");
    }

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException("Saved jobs retrieval not implemented for Tecnoempleo");
    }


}