using Ghost.Contracts.Jobs;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Ghost.Worker;

/// <summary>
/// Worker that pulls scraping jobs from Redis queue and executes them.
/// </summary>
public sealed partial class ScraperWorker : BackgroundService
{
    private readonly ILogger<ScraperWorker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerConfiguration _config;
    private readonly SemaphoreSlim _concurrencyLimiter;

    // LoggerMessage delegates for high-performance logging
    [LoggerMessage(Level = LogLevel.Information, Message = "Ghost Worker {WorkerId} starting on node {NodeName} with max concurrency {MaxConcurrency}")]
    private partial void LogWorkerStarting(string workerId, string nodeName, int maxConcurrency);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing job {JobId} for platform {Platform}, query: {Query}")]
    private partial void LogProcessingJob(string jobId, string platform, string query);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed job {JobId} in {DurationMs}ms, found {ResultCount} results")]
    private partial void LogJobCompleted(string jobId, double durationMs, int resultCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker stopping due to cancellation")]
    private partial void LogWorkerStopping();

    [LoggerMessage(Level = LogLevel.Information, Message = "Ghost Worker {WorkerId} stopping")]
    private partial void LogWorkerShutdown(string workerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled error processing job")]
    private partial void LogUnhandledJobError(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in worker loop")]
    private partial void LogWorkerLoopError(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process job {JobId} after {DurationMs}ms")]
    private partial void LogJobFailed(Exception ex, string jobId, double durationMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stored {Count} results for job {JobId}")]
    private partial void LogResultsStored(int count, string jobId);

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
        LogWorkerStarting(_config.WorkerId, _config.NodeName, _config.MaxConcurrentJobs);

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
                        LogUnhandledJobError(ex);
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                LogWorkerStopping();
                break;
            }
            catch (Exception ex)
            {
                LogWorkerLoopError(ex);
                await Task.Delay(1000, stoppingToken); // Brief pause before retry
            }
        }

        LogWorkerShutdown(_config.WorkerId);
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
            LogProcessingJob(jobRequest.JobId, jobRequest.Platform, jobRequest.SearchQuery);

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
            LogJobCompleted(jobRequest.JobId, duration.TotalMilliseconds, results.Count);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            LogJobFailed(ex, jobId, duration.TotalMilliseconds);

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

        LogResultsStored(results.Count, jobId);
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
