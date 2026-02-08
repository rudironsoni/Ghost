using System.Text.Json;
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
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;

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
        ILogger<RedisJobDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        _redis = ConnectionMultiplexer.Connect(_options.ConnectionString);
        _db = _redis.GetDatabase(_options.Database);

        s_queueInitialized(_logger, _options.ConnectionString, null);
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(Job job, int priority = 2, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        job.Priority = (JobPriority)Math.Clamp(priority, 0, 3);
        job.CreatedAt = DateTime.UtcNow;
        job.RetryCount = 0;

        var key = GetPendingKey(job.Priority);
        var jobJson = JsonSerializer.Serialize(job, s_jsonOptions);

        // Use current timestamp as score for FIFO within priority
        var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _db.SortedSetAddAsync(key, jobJson, score);

        s_jobEnqueued(_logger, job.Id, job.Priority.ToString(), null);

        return job.Id;
    }

    /// <inheritdoc />
    public async Task<Job?> DequeueAsync(string workerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        // Try to dequeue from each priority level
        for (int priority = 0; priority <= 3; priority++)
        {
            var key = GetPendingKey((JobPriority)priority);

            // Get and remove the job with lowest score (oldest)
            var entries = await _db.SortedSetRangeByScoreWithScoresAsync(key, take: 1);

            if (entries.Length == 0)
                continue;

            var entry = entries[0];
            var removed = await _db.SortedSetRemoveAsync(key, entry.Element);

            if (!removed)
                continue; // Another worker got it first

            var job = JsonSerializer.Deserialize<Job>(entry.Element.ToString(), s_jsonOptions);
            if (job == null)
                continue;

            job.WorkerId = workerId;
            job.LastAttemptAt = DateTime.UtcNow;

            // Add to active jobs
            var activeKey = GetActiveKey(workerId);
            var jobJson = JsonSerializer.Serialize(job, s_jsonOptions);
            await _db.HashSetAsync(activeKey, job.Id, jobJson);

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

        result.JobId = jobId;
        result.CompletedAt = DateTime.UtcNow;

        // Remove from active jobs
        var activeKey = GetActiveKey(result.WorkerId ?? "unknown");
        await _db.HashDeleteAsync(activeKey, jobId);

        // Add to completed jobs
        var completedKey = GetCompletedKey();
        var resultJson = JsonSerializer.Serialize(result, s_jsonOptions);
        await _db.ListLeftPushAsync(completedKey, resultJson);

        // Trim completed list to max size
        await _db.ListTrimAsync(completedKey, 0, _options.MaxCompletedHistory - 1);

        s_jobCompleted(_logger, jobId, null);
    }

    /// <inheritdoc />
    public async Task FailAsync(string jobId, Exception exception, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(exception);

        // Get job from active queue
        Job? job = null;
        string? workerId = null;

        // Search all active worker queues to find the job
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"{_options.QueuePrefix}:active:*";
        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            var jobJson = await _db.HashGetAsync(key, jobId);
            if (!jobJson.IsNullOrEmpty)
            {
                job = JsonSerializer.Deserialize<Job>(jobJson.ToString(), s_jsonOptions);
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
        job.LastAttemptAt = DateTime.UtcNow;

        // Remove from active jobs
        var activeKey = GetActiveKey(workerId ?? "unknown");
        await _db.HashDeleteAsync(activeKey, jobId);

        if (job.RetryCount >= job.MaxRetries)
        {
            // Move to dead letter queue
            var deadKey = GetDeadKey();
            var jobJson = JsonSerializer.Serialize(job, s_jsonOptions);
            await _db.ListLeftPushAsync(deadKey, jobJson);

            s_jobMovedToDead(_logger, jobId, job.RetryCount, exception.Message, null);
        }
        else
        {
            // Calculate exponential backoff: 2^attempt minutes
            var delayMinutes = Math.Pow(2, job.RetryCount);
            var retryAt = DateTimeOffset.UtcNow.AddMinutes(delayMinutes).ToUnixTimeMilliseconds();

            // Re-enqueue with delay (using score as timestamp)
            var key = GetPendingKey(job.Priority);
            var jobJson = JsonSerializer.Serialize(job, s_jsonOptions);
            await _db.SortedSetAddAsync(key, jobJson, retryAt);

            s_jobRetryScheduled(_logger, jobId, job.RetryCount, job.MaxRetries, delayMinutes, exception.Message, null);
        }

        // Store failure details
        var failedKey = GetFailedKey(jobId);
        await _db.HashSetAsync(failedKey, new HashEntry[]
        {
            new("job_id", jobId),
            new("retry_count", job.RetryCount),
            new("error", exception.Message),
            new("stack_trace", exception.StackTrace ?? ""),
            new("failed_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        });
        await _db.KeyExpireAsync(failedKey, TimeSpan.FromDays(7)); // Keep for 7 days
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        long total = 0;
        for (int priority = 0; priority <= 3; priority++)
        {
            var key = GetPendingKey((JobPriority)priority);
            var count = await _db.SortedSetLengthAsync(key);
            total += count;
        }
        return (int)total;
    }

    /// <inheritdoc />
    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        long total = 0;
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"{_options.QueuePrefix}:active:*";

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            var count = await _db.HashLengthAsync(key);
            total += count;
        }

        return (int)total;
    }

    /// <inheritdoc />
    public async Task<int> GetCompletedCountAsync(CancellationToken cancellationToken = default)
    {
        var key = GetCompletedKey();
        var count = await _db.ListLengthAsync(key);
        return (int)count;
    }

    /// <inheritdoc />
    public async Task<int> GetDeadCountAsync(CancellationToken cancellationToken = default)
    {
        var key = GetDeadKey();
        var count = await _db.ListLengthAsync(key);
        return (int)count;
    }

    private string GetPendingKey(JobPriority priority) => $"{_options.QueuePrefix}:pending:{(int)priority}";
    private string GetActiveKey(string workerId) => $"{_options.QueuePrefix}:active:{workerId}";
    private string GetCompletedKey() => $"{_options.QueuePrefix}:completed";
    private string GetDeadKey() => $"{_options.QueuePrefix}:dead";
    private string GetFailedKey(string jobId) => $"{_options.QueuePrefix}:failed:{jobId}";

    public async ValueTask DisposeAsync()
    {
        await _redis.DisposeAsync();
        s_queueDisposed(_logger, null);
    }
}
