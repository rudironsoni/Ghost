using Ghost.Contracts.Jobs;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Ghost.Worker;

/// <summary>
/// Worker that pulls scraping jobs from Redis queue and executes them.
/// </summary>
public sealed class ScraperWorker : BackgroundService
{
    private readonly ILogger<ScraperWorker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerConfiguration _config;
    private readonly SemaphoreSlim _concurrencyLimiter;

    public ScraperWorker(
        ILogger<ScraperWorker> logger,
        IConnectionMultiplexer redis,
        IServiceProvider serviceProvider,
        WorkerConfiguration config)
    {
        _logger = logger;
        _redis = redis;
        _serviceProvider = serviceProvider;
        _config = config;
        _concurrencyLimiter = new SemaphoreSlim(config.MaxConcurrentJobs, config.MaxConcurrentJobs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Ghost Worker {WorkerId} starting on node {NodeName} with max concurrency {MaxConcurrency}",
            _config.WorkerId,
            _config.NodeName,
            _config.MaxConcurrentJobs
        );

        var db = _redis.GetDatabase();
        var queueKey = _config.RedisQueueKey;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for available concurrency slot
                await _concurrencyLimiter.WaitAsync(stoppingToken);

                // Pop job from Redis queue (blocking with timeout)
                var jobJson = await db.ListRightPopAsync(queueKey);

                if (jobJson.IsNullOrEmpty)
                {
                    // No jobs available, release slot and wait before retry
                    _concurrencyLimiter.Release();
                    await Task.Delay(_config.PollIntervalMs, stoppingToken);
                    continue;
                }

                // Process job asynchronously (fire and forget with error handling)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessJobAsync(jobJson!, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled error processing job");
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
                await Task.Delay(1000, stoppingToken); // Brief pause before retry
            }
        }

        _logger.LogInformation("Ghost Worker {WorkerId} stopping", _config.WorkerId);
    }

    private async Task ProcessJobAsync(string jobJson, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        string jobId = "unknown";

        try
        {
            // Deserialize job request
            ArgumentNullException.ThrowIfNull(JsonConvert.DeserializeObject<JobRequest>(jobJson), nameof(jobJson));
            var jobRequest = JsonConvert.DeserializeObject<JobRequest>(jobJson)!;

            jobId = jobRequest.JobId;
            _logger.LogInformation(
                "Processing job {JobId} for platform {Platform}, query: {Query}",
                jobRequest.JobId,
                jobRequest.Platform,
                jobRequest.SearchQuery
            );

            // Update job status to processing
            await UpdateJobStatusAsync(jobRequest.JobId, JobStatus.Processing, cancellationToken);

            // Resolve the appropriate job client for the platform
            var jobClient = ResolveJobClient(jobRequest.Platform) 
                ?? throw new NotSupportedException($"Platform {jobRequest.Platform} is not supported");

            // Execute scraping with platform-specific criteria
            var criteria = new JobSearchCriteria
            {
                Query = jobRequest.SearchQuery,
                Location = jobRequest.Location,
                MaxResults = jobRequest.MaxResults
            };

            var results = await jobClient.SearchJobsAsync(criteria, cancellationToken);

            // Store results
            await StoreResultsAsync(jobRequest.JobId, results, cancellationToken);

            // Update job status to completed
            await UpdateJobStatusAsync(jobRequest.JobId, JobStatus.Completed, cancellationToken);

            var duration = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation(
                "Completed job {JobId} in {Duration}ms, found {ResultCount} results",
                jobRequest.JobId,
                duration.TotalMilliseconds,
                results.Count
            );
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _logger.LogError(
                ex,
                "Failed to process job {JobId} after {Duration}ms",
                jobId,
                duration.TotalMilliseconds
            );

            // Update job status to failed
            await UpdateJobStatusAsync(jobId, JobStatus.Failed, cancellationToken, ex.Message);
        }
    }

    private IJobClient? ResolveJobClient(string platform)
    {
        // Use service provider to resolve the correct IJobClient implementation
        // based on the platform name
        using var scope = _serviceProvider.CreateScope();

        return platform.ToLowerInvariant() switch
        {
            "linkedin" => scope.ServiceProvider.GetKeyedService<IJobClient>("linkedin"),
            "indeed" => scope.ServiceProvider.GetKeyedService<IJobClient>("indeed"),
            "glassdoor" => scope.ServiceProvider.GetKeyedService<IJobClient>("glassdoor"),
            _ => null
        };
    }

    private async Task StoreResultsAsync(string jobId, IReadOnlyList<JobListing> results, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var resultsKey = $"job:results:{jobId}";

        // Store results as JSON in Redis (with expiration)
        var resultsJson = JsonConvert.SerializeObject(results);
        await db.StringSetAsync(resultsKey, resultsJson, TimeSpan.FromHours(_config.ResultsExpirationHours));

        _logger.LogDebug("Stored {Count} results for job {JobId}", results.Count, jobId);
    }

    private async Task UpdateJobStatusAsync(
        string jobId,
        JobStatus status,
        CancellationToken cancellationToken,
        string? errorMessage = null)
    {
        var db = _redis.GetDatabase();
        var statusKey = $"job:status:{jobId}";

        var statusData = new
        {
            JobId = jobId,
            Status = status.ToString(),
            UpdatedAt = DateTimeOffset.UtcNow,
            ErrorMessage = errorMessage
        };

        var statusJson = JsonConvert.SerializeObject(statusData);
        await db.StringSetAsync(statusKey, statusJson, TimeSpan.FromHours(_config.ResultsExpirationHours));
    }

    public override void Dispose()
    {
        _concurrencyLimiter.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Job request model for Redis queue.
/// </summary>
public sealed class JobRequest
{
    public string JobId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int MaxResults { get; set; } = 50;
}

/// <summary>
/// Job status enumeration.
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
