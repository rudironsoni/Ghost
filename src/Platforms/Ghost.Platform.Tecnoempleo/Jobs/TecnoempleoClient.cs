using Ghost.Contracts.Jobs;
using Ghost.Platform.Tecnoempleo.Jobs.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Ghost.Platform.Tecnoempleo.Jobs;

public class TecnoempleoClient : IJobClient
{
    private readonly TecnoempleoApiClient _apiClient;
    private readonly TecnoempleoOptions _options;
    private readonly ILogger<TecnoempleoClient> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

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
                    _logger.LogWarning(exception, "Retry {RetryCount} for Tecnoempleo API call", retryCount);
                });
    }

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var query = criteria.Query ?? string.Empty;
                var location = criteria.Location ?? string.Empty;
                var maxResults = criteria.MaxResults;
                var pageSize = Math.Min(maxResults, 20);
                var page = 1;

                var jobs = await _apiClient.SearchJobsAsync(query, location, page, pageSize);
                
                _logger.LogInformation("Found {JobCount} Tecnoempleo jobs for query: {Query}", jobs.Count, query);
                
                return jobs.Take(maxResults).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Tecnoempleo jobs for query: {Query}", criteria.Query);
            return new List<JobListing>();
        }
    }

    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var job = await _apiClient.GetJobDetailsAsync(jobId);
                
                _logger.LogInformation("Retrieved Tecnoempleo job details for ID: {JobId}", jobId);
                
                return job;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Tecnoempleo job details for ID: {JobId}", jobId);
            throw;
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