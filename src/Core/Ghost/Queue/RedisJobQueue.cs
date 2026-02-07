using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ghost.Queue;

/// <summary>
/// Redis-based job queue implementation
/// </summary>
public sealed class RedisJobQueue : IJobQueue, IAsyncDisposable
{
    private readonly RedisQueueOptions _options;
    private readonly ILogger<RedisJobQueue> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public RedisJobQueue(
        IOptions<RedisQueueOptions> options,
        ILogger<RedisJobQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        _redis = ConnectionMultiplexer.Connect(_options.ConnectionString);
        _db = _redis.GetDatabase(_options.Database);

        _logger.LogInformation("Redis job queue initialized with connection: {Connection}",
            _options.ConnectionString);
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(Job job, int priority = 2, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        job.Priority = (JobPriority)Math.Clamp(priority, 0, 3);
        job.CreatedAt = DateTime.UtcNow;
        job.RetryCount = 0;

        var key = GetPendingKey(job.Priority);
        var jobJson = JsonSerializer.Serialize(job, _jsonOptions);

        // Use current timestamp as score for FIFO within priority
        var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _db.SortedSetAddAsync(key, jobJson, score);

        _logger.LogDebug("Enqueued job {JobId} with priority {Priority}", job.Id, job.Priority);

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

            var job = JsonSerializer.Deserialize<Job>(entry.Element.ToString(), _jsonOptions);
            if (job == null)
                continue;

            job.WorkerId = workerId;
            job.LastAttemptAt = DateTime.UtcNow;

            // Add to active jobs
            var activeKey = GetActiveKey(workerId);
            var jobJson = JsonSerializer.Serialize(job, _jsonOptions);
            await _db.HashSetAsync(activeKey, job.Id, jobJson);

            _logger.LogDebug("Dequeued job {JobId} for worker {WorkerId}", job.Id, workerId);

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
        var resultJson = JsonSerializer.Serialize(result, _jsonOptions);
        await _db.ListLeftPushAsync(completedKey, resultJson);

        // Trim completed list to max size
        await _db.ListTrimAsync(completedKey, 0, _options.MaxCompletedHistory - 1);

        _logger.LogInformation("Completed job {JobId} successfully", jobId);
    }

    /// <inheritdoc />
    public async Task FailAsync(string jobId, Exception error, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(error);

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
                job = JsonSerializer.Deserialize<Job>(jobJson.ToString(), _jsonOptions);
                workerId = key.ToString().Split(':').Last();
                break;
            }
        }

        if (job == null)
        {
            _logger.LogWarning("Failed job {JobId} not found in active queue", jobId);
            return;
        }

        job.RetryCount++;
        job.LastError = error.Message;
        job.LastAttemptAt = DateTime.UtcNow;

        // Remove from active jobs
        var activeKey = GetActiveKey(workerId ?? "unknown");
        await _db.HashDeleteAsync(activeKey, jobId);

        if (job.RetryCount >= job.MaxRetries)
        {
            // Move to dead letter queue
            var deadKey = GetDeadKey();
            var jobJson = JsonSerializer.Serialize(job, _jsonOptions);
            await _db.ListLeftPushAsync(deadKey, jobJson);

            _logger.LogError("Job {JobId} moved to dead letter queue after {RetryCount} retries. Error: {Error}",
                jobId, job.RetryCount, error.Message);
        }
        else
        {
            // Calculate exponential backoff: 2^attempt minutes
            var delayMinutes = Math.Pow(2, job.RetryCount);
            var retryAt = DateTimeOffset.UtcNow.AddMinutes(delayMinutes).ToUnixTimeMilliseconds();

            // Re-enqueue with delay (using score as timestamp)
            var key = GetPendingKey(job.Priority);
            var jobJson = JsonSerializer.Serialize(job, _jsonOptions);
            await _db.SortedSetAddAsync(key, jobJson, retryAt);

            _logger.LogWarning("Job {JobId} failed, retry {RetryCount}/{MaxRetries} scheduled in {Delay} minutes. Error: {Error}",
                jobId, job.RetryCount, job.MaxRetries, delayMinutes, error.Message);
        }

        // Store failure details
        var failedKey = GetFailedKey(jobId);
        await _db.HashSetAsync(failedKey, new HashEntry[]
        {
            new("job_id", jobId),
            new("retry_count", job.RetryCount),
            new("error", error.Message),
            new("stack_trace", error.StackTrace ?? ""),
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
        _logger.LogInformation("Redis job queue disposed");
    }
}
