using Ghost.Contracts.Jobs;
using Ghost.Redis;
using StackExchange.Redis;
using System.Text.Json;

namespace Ghost.Worker;

/// <summary>
/// Worker that pulls scraping jobs from Redis queue and executes them.
/// </summary>
public sealed partial class ScraperWorker : BackgroundService
{
    private readonly ILogger<ScraperWorker> _logger;
    private readonly RedisConnectionFactory _redisFactory;
    private IConnectionMultiplexer? _redis;
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerConfiguration _config;
    private readonly TimeProvider _timeProvider;

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
        RedisConnectionFactory redisFactory,
        IServiceProvider serviceProvider,
        WorkerConfiguration config,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _redisFactory = redisFactory;
        _serviceProvider = serviceProvider;
        _config = config;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarting(_config.WorkerId, _config.NodeName, _config.MaxConcurrentJobs);

        // Connect to Redis asynchronously (no sync-over-async)
        _redis = await _redisFactory.ConnectAsync(stoppingToken).ConfigureAwait(false);
        IDatabase db = _redis.GetDatabase();
        string queueKey = _config.RedisQueueKey;

        Task[] workers = Enumerable
            .Range(0, _config.MaxConcurrentJobs)
            .Select(_ => RunWorkerLoopAsync(db, queueKey, stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogWorkerStopping();
        }

        LogWorkerShutdown(_config.WorkerId);
    }

    private async Task RunWorkerLoopAsync(IDatabase db, string queueKey, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RedisValue jobJson = await db.ListRightPopAsync(queueKey).ConfigureAwait(false);

                if (jobJson.IsNullOrEmpty)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(_config.PollIntervalMs), _timeProvider, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await ProcessJobAsync(jobJson!, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogUnhandledJobError(ex);
                await Task.Delay(TimeSpan.FromMilliseconds(1000), _timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessJobAsync(string jobJson, CancellationToken cancellationToken)
    {
        DateTimeOffset startTime = new DateTimeOffset(_timeProvider.GetUtcNow().DateTime, TimeSpan.Zero);
        string jobId = "unknown";

        try
        {
            // Deserialize job request
            JobRequest jobRequest = JsonSerializer.Deserialize<JobRequest>(jobJson)
                ?? throw new ArgumentNullException(nameof(jobJson));

            jobId = jobRequest.JobId;
            LogProcessingJob(jobRequest.JobId, jobRequest.Platform, jobRequest.SearchQuery);

            // Update job status to processing
            await UpdateJobStatusAsync(jobRequest.JobId, JobStatus.Processing, cancellationToken).ConfigureAwait(false);

            // Resolve the appropriate job client for the platform
#pragma warning disable CA2007 // CreateAsyncScope returns AsyncServiceScope, not Task - await applies to disposal only
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
#pragma warning restore CA2007
            IJobClient jobClient = ResolveJobClient(scope.ServiceProvider, jobRequest.Platform)
                ?? throw new NotSupportedException($"Platform '{jobRequest.Platform}' is not supported");

            // Execute scraping with platform-specific criteria
            var criteria = new JobSearchCriteria
            {
                Query = jobRequest.SearchQuery,
                Location = jobRequest.Location,
                MaxResults = jobRequest.MaxResults
            };

            IReadOnlyList<JobListing> results = await jobClient.SearchJobsAsync(criteria, cancellationToken).ConfigureAwait(false);

            // Store results
            await StoreResultsAsync(jobRequest.JobId, results, cancellationToken).ConfigureAwait(false);

            // Update job status to completed
            await UpdateJobStatusAsync(jobRequest.JobId, JobStatus.Completed, cancellationToken).ConfigureAwait(false);

            TimeSpan duration = new DateTimeOffset(_timeProvider.GetUtcNow().DateTime, TimeSpan.Zero) - startTime;
            LogJobCompleted(jobRequest.JobId, duration.TotalMilliseconds, results.Count);
        }
        catch (Exception ex)
        {
            TimeSpan duration = new DateTimeOffset(_timeProvider.GetUtcNow().DateTime, TimeSpan.Zero) - startTime;
            LogJobFailed(ex, jobId, duration.TotalMilliseconds);

            // Update job status to failed
            await UpdateJobStatusAsync(jobId, JobStatus.Failed, cancellationToken, ex.Message).ConfigureAwait(false);
        }
    }

    private static IJobClient? ResolveJobClient(IServiceProvider scopedProvider, string platform)
    {
        return platform.ToLowerInvariant() switch
        {
            "linkedin" => scopedProvider.GetKeyedService<IJobClient>("linkedin"),
            "indeed" => scopedProvider.GetKeyedService<IJobClient>("indeed"),
            "glassdoor" => scopedProvider.GetKeyedService<IJobClient>("glassdoor"),
            _ => null
        };
    }

    private async Task StoreResultsAsync(string jobId, IReadOnlyList<JobListing> results, CancellationToken cancellationToken)
    {
        IDatabase db = _redis!.GetDatabase();
        string resultsKey = $"job:results:{jobId}";

        // Store results as JSON in Redis (with expiration)
        string resultsJson = JsonSerializer.Serialize(results);
        await db.StringSetAsync(resultsKey, resultsJson, TimeSpan.FromHours(_config.ResultsExpirationHours)).ConfigureAwait(false);

        LogResultsStored(results.Count, jobId);
    }

    private async Task UpdateJobStatusAsync(
        string jobId,
        JobStatus status,
        CancellationToken cancellationToken,
        string? errorMessage = null)
    {
        IDatabase db = _redis!.GetDatabase();
        string statusKey = $"job:status:{jobId}";

        var statusData = new
        {
            JobId = jobId,
            Status = status.ToString(),
            UpdatedAt = new DateTimeOffset(_timeProvider.GetUtcNow().DateTime, TimeSpan.Zero),
            ErrorMessage = errorMessage
        };

        string statusJson = JsonSerializer.Serialize(statusData);
        await db.StringSetAsync(statusKey, statusJson, TimeSpan.FromHours(_config.ResultsExpirationHours)).ConfigureAwait(false);
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
