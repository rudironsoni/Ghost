using System.Text.Json;
using Ghost.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ghost.Queue;

/// <summary>
/// Redis-based job dispatcher implementation
/// </summary>
public sealed class RedisJobDispatcher : IJobDispatcher, IAsyncDisposable
{
    private readonly RedisQueueOptions _options;
    private readonly ILogger<RedisJobDispatcher> _logger;
    private readonly TimeProvider _timeProvider;
    private ConnectionMultiplexer? _redis;
    private IDatabase? _db;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private static readonly Action<ILogger, string, string, Exception?> s_jobEnqueued =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1, "JobEnqueued"),
            "Enqueued job {JobId} with priority {Priority}");

    private static readonly Action<ILogger, string, string, Exception?> s_jobDequeued =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(2, "JobDequeued"),
            "Dequeued job {JobId} for worker {WorkerId}");

    private static readonly Action<ILogger, string, Exception?> s_jobCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "JobCompleted"),
            "Completed job {JobId} successfully");

    private static readonly Action<ILogger, string, Exception?> s_jobNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "JobNotFound"),
            "Failed job {JobId} not found in active queue");

    private static readonly Action<ILogger, string, int, string, Exception?> s_jobMovedToDead =
        LoggerMessage.Define<string, int, string>(LogLevel.Error, new EventId(5, "JobMovedToDead"),
            "Job {JobId} moved to dead letter queue after {RetryCount} retries. Error: {Error}");

    private static readonly Action<ILogger, string, int, int, double, string, Exception?> s_jobRetryScheduled =
        LoggerMessage.Define<string, int, int, double, string>(LogLevel.Warning, new EventId(6, "JobRetryScheduled"),
            "Job {JobId} failed, retry {RetryCount}/{MaxRetries} scheduled in {Delay} minutes. Error: {Error}");

    private static readonly Action<ILogger, string, Exception?> s_queueInitialized =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(7, "QueueInitialized"),
            "Redis job queue initialized with connection: {Connection}");

    private static readonly Action<ILogger, Exception?> s_queueDisposed =
        LoggerMessage.Define(LogLevel.Information, new EventId(8, "QueueDisposed"),
            "Redis job queue disposed");

    public RedisJobDispatcher(
        IOptions<RedisQueueOptions> options,
        ILogger<RedisJobDispatcher> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Ensures the Redis connection is established asynchronously.
    /// Uses double-checked locking pattern for thread-safe lazy initialization.
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (_redis is not null)
        {
            return;
        }

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_redis is null)
            {
                _redis = await ConnectionMultiplexer.ConnectAsync(_options.ConnectionString).ConfigureAwait(false);
                _db = _redis.GetDatabase(_options.Database);
                s_queueInitialized(_logger, _options.ConnectionString, null);
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(Job job, int priority = 2, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        job.Priority = (JobPriority)Math.Clamp(priority, 0, 3);
        job.CreatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        job.RetryCount = 0;

        string key = GetPendingKey(job.Priority);
        string jobJson = JsonSerializer.Serialize(job, KernelSerializerContext.Default.Job);

        // Use current timestamp as score for FIFO within priority
        long score = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        await _db!.SortedSetAddAsync(key, jobJson, score).ConfigureAwait(false);

        s_jobEnqueued(_logger, job.Id, job.Priority.ToString(), null);

        return job.Id;
    }

    /// <inheritdoc />
    public async Task<Job?> DequeueAsync(string workerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        // Try to dequeue from each priority level
        for (int priority = 0; priority <= 3; priority++)
        {
            string key = GetPendingKey((JobPriority)priority);

            // Get and remove the job with lowest score (oldest)
            SortedSetEntry[] entries = await _db!.SortedSetRangeByScoreWithScoresAsync(key, take: 1).ConfigureAwait(false);

            if (entries.Length == 0)
                continue;

            SortedSetEntry entry = entries[0];
            bool removed = await _db!.SortedSetRemoveAsync(key, entry.Element).ConfigureAwait(false);

            if (!removed)
                continue; // Another worker got it first

            Job? job = JsonSerializer.Deserialize(entry.Element.ToString(), KernelSerializerContext.Default.Job);
            if (job == null)
                continue;

            job.WorkerId = workerId;
            job.LastAttemptAt = _timeProvider.GetUtcNow().UtcDateTime;

            // Add to active jobs
            string activeKey = GetActiveKey(workerId);
            string jobJson = JsonSerializer.Serialize(job, KernelSerializerContext.Default.Job);
            await _db!.HashSetAsync(activeKey, job.Id, jobJson).ConfigureAwait(false);

            s_jobDequeued(_logger, job.Id, workerId, null);

            return job;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(string jobId, JobResult result, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(result);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        result.JobId = jobId;
        result.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;

        // Remove from active jobs
        string activeKey = GetActiveKey(result.WorkerId ?? "unknown");
        await _db!.HashDeleteAsync(activeKey, jobId).ConfigureAwait(false);

        // Add to completed jobs
        string completedKey = GetCompletedKey();
        string resultJson = JsonSerializer.Serialize(result, KernelSerializerContext.Default.JobResult);
        await _db!.ListLeftPushAsync(completedKey, resultJson).ConfigureAwait(false);

        // Trim completed list to max size
        await _db!.ListTrimAsync(completedKey, 0, _options.MaxCompletedHistory - 1).ConfigureAwait(false);

        s_jobCompleted(_logger, jobId, null);
    }

    /// <inheritdoc />
    public async Task FailAsync(string jobId, Exception exception, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(exception);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        // Get job from active queue
        Job? job = null;
        string? workerId = null;

        // Search all active worker queues to find the job
        IServer server = _redis!.GetServer(_redis.GetEndPoints().First());
        string pattern = $"{_options.QueuePrefix}:active:*";
        await foreach (RedisKey key in server.KeysAsync(pattern: pattern).ConfigureAwait(false))
        {
            RedisValue jobJson = await _db!.HashGetAsync(key, jobId).ConfigureAwait(false);
            if (!jobJson.IsNullOrEmpty)
            {
                job = JsonSerializer.Deserialize(jobJson.ToString(), KernelSerializerContext.Default.Job);
                workerId = key.ToString().Split(':').Last();
                break;
            }
        }

        if (job == null)
        {
            s_jobNotFound(_logger, jobId, null);
            return;
        }

        job.RetryCount++;
        job.LastError = exception.Message;
        job.LastAttemptAt = _timeProvider.GetUtcNow().UtcDateTime;

        // Remove from active jobs
        string activeKey = GetActiveKey(workerId ?? "unknown");
        await _db!.HashDeleteAsync(activeKey, jobId).ConfigureAwait(false);

        if (job.RetryCount >= job.MaxRetries)
        {
            // Move to dead letter queue
            string deadKey = GetDeadKey();
            string jobJson = JsonSerializer.Serialize(job, KernelSerializerContext.Default.Job);
            await _db!.ListLeftPushAsync(deadKey, jobJson).ConfigureAwait(false);

            s_jobMovedToDead(_logger, jobId, job.RetryCount, exception.Message, null);
        }
        else
        {
            // Calculate exponential backoff: 2^attempt minutes
            double delayMinutes = Math.Pow(2, job.RetryCount);
            long retryAt = _timeProvider.GetUtcNow().AddMinutes(delayMinutes).ToUnixTimeMilliseconds();

            // Re-enqueue with delay (using score as timestamp)
            string key = GetPendingKey(job.Priority);
            string jobJson = JsonSerializer.Serialize(job, KernelSerializerContext.Default.Job);
            await _db!.SortedSetAddAsync(key, jobJson, retryAt).ConfigureAwait(false);

            s_jobRetryScheduled(_logger, jobId, job.RetryCount, job.MaxRetries, delayMinutes, exception.Message, null);
        }

        // Store failure details
        string failedKey = GetFailedKey(jobId);
        await _db!.HashSetAsync(failedKey, new HashEntry[]
        {
            new("job_id", jobId),
            new("retry_count", job.RetryCount),
            new("error", exception.Message),
            new("stack_trace", exception.StackTrace ?? ""),
            new("failed_at", _timeProvider.GetUtcNow().ToUnixTimeSeconds())
        }).ConfigureAwait(false);
        await _db!.KeyExpireAsync(failedKey, TimeSpan.FromDays(7)).ConfigureAwait(false); // Keep for 7 days
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        long total = 0;
        for (int priority = 0; priority <= 3; priority++)
        {
            string key = GetPendingKey((JobPriority)priority);
            long count = await _db!.SortedSetLengthAsync(key).ConfigureAwait(false);
            total += count;
        }
        return (int)total;
    }

    /// <inheritdoc />
    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        long total = 0;
        IServer server = _redis!.GetServer(_redis.GetEndPoints().First());
        string pattern = $"{_options.QueuePrefix}:active:*";

        await foreach (RedisKey key in server.KeysAsync(pattern: pattern).ConfigureAwait(false))
        {
            long count = await _db!.HashLengthAsync(key).ConfigureAwait(false);
            total += count;
        }

        return (int)total;
    }

    /// <inheritdoc />
    public async Task<int> GetCompletedCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        string key = GetCompletedKey();
        long count = await _db!.ListLengthAsync(key).ConfigureAwait(false);
        return (int)count;
    }

    /// <inheritdoc />
    public async Task<int> GetDeadCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        string key = GetDeadKey();
        long count = await _db!.ListLengthAsync(key).ConfigureAwait(false);
        return (int)count;
    }

    private string GetPendingKey(JobPriority priority) => $"{_options.QueuePrefix}:pending:{(int)priority}";
    private string GetActiveKey(string workerId) => $"{_options.QueuePrefix}:active:{workerId}";
    private string GetCompletedKey() => $"{_options.QueuePrefix}:completed";
    private string GetDeadKey() => $"{_options.QueuePrefix}:dead";
    private string GetFailedKey(string jobId) => $"{_options.QueuePrefix}:failed:{jobId}";

    public async ValueTask DisposeAsync()
    {
        // Only dispose if connection was actually established
        if (_redis is not null)
        {
            await _redis.DisposeAsync().ConfigureAwait(false);
            s_queueDisposed(_logger, null);
        }
    }
}
