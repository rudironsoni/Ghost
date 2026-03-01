using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Kernel;
using Ghost.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghost.Resilience;

/// <summary>
/// Configuration options for the file-system backed dead letter queue.
/// </summary>
public sealed class DeadLetterQueueOptions
{
    /// <summary>
    /// Gets or sets the root path for dead letter queue storage.
    /// </summary>
    public string RootPath { get; set; } = "/var/ghost/dlq";

    /// <summary>
    /// Gets or sets how old a job must be before automatic archiving occurs.
    /// </summary>
    public TimeSpan AutoArchiveAfter { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the minimum interval between automatic archive sweeps.
    /// </summary>
    public TimeSpan ArchiveCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// File-system based dead letter queue implementation using JSON storage.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Dead letter queue naming aligns with domain terminology.")]
public sealed class FileSystemDeadLetterQueue : IGenericDeadLetterStore
{
    private static readonly Action<ILogger, string, Exception?> s_logReadFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(ReadJobAsync)), "Failed to read dead letter job file {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logWriteFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, nameof(WriteJobAsync)), "Failed to write dead letter job file {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logArchiveFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3, nameof(ArchiveAsync)), "Failed to archive dead letter job file {Path}");

    private readonly DeadLetterQueueOptions _options;
    private readonly ILogger<FileSystemDeadLetterQueue> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _activePath;
    private readonly string _archivePath;
    private readonly object _archiveGate = new();
    private DateTime _lastArchiveCheckUtc = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDeadLetterQueue"/> class.
    /// </summary>
    /// <param name="rootPath">The root path for DLQ storage.</param>
    public FileSystemDeadLetterQueue(string rootPath)
        : this(new DeadLetterQueueOptions { RootPath = rootPath })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDeadLetterQueue"/> class.
    /// </summary>
    /// <param name="options">Dead letter queue options.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="timeProvider">Optional time provider instance.</param>
    public FileSystemDeadLetterQueue(DeadLetterQueueOptions options, ILogger<FileSystemDeadLetterQueue>? logger = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        string rootPath = options.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("RootPath must be provided.", nameof(options));
        }

        _options = new DeadLetterQueueOptions
        {
            RootPath = Path.GetFullPath(rootPath),
            AutoArchiveAfter = options.AutoArchiveAfter,
            ArchiveCheckInterval = options.ArchiveCheckInterval
        };
        _logger = logger ?? NullLogger<FileSystemDeadLetterQueue>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _activePath = Path.Combine(_options.RootPath, "active");
        _archivePath = Path.Combine(_options.RootPath, "archived");
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        EnsureDirectories();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDeadLetterQueue"/> class.
    /// </summary>
    /// <param name="options">Dead letter queue options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider instance.</param>
    public FileSystemDeadLetterQueue(IOptions<DeadLetterQueueOptions> options, ILogger<FileSystemDeadLetterQueue> logger, TimeProvider? timeProvider = null)
        : this(options?.Value ?? new DeadLetterQueueOptions(), logger, timeProvider)
    {
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(FailedScrapeJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(job.Id))
        {
            job.Id = Guid.NewGuid().ToString("N")[..8];
        }

        if (job.FailedAt == default)
        {
            job.FailedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        if (job.Platform is null)
        {
            job.Platform = string.Empty;
        }

        string path = GetActiveJobPath(job);
        await WriteJobAsync(path, job).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnqueueAsync<T>(T item, string reason, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        var job = new FailedScrapeJob
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Platform = typeof(T).Name,
            Error = reason + (exception != null ? $": {exception.Message}" : string.Empty),
            FailedAt = _timeProvider.GetUtcNow().UtcDateTime,
            StackTrace = exception?.StackTrace ?? string.Empty
        };

        var metadata = new Dictionary<string, object>
        {
            ["Data"] = JsonSerializer.Serialize(item)
        };

        job.Metadata = metadata;

        string path = GetActiveJobPath(job);
        await WriteJobAsync(path, job).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<Ghost.Kernel.DeadLetterItem>> PeekAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        List<Ghost.Kernel.DeadLetterItem> items = [];

        foreach (string? path in EnumerateActiveFiles().Take(count))
        {
            FailedScrapeJob? job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null) continue;

            string data = job.Metadata?.GetValueOrDefault("Data")?.ToString() ?? string.Empty;

            items.Add(new Ghost.Kernel.DeadLetterItem
            {
                Id = Guid.TryParse(job.Id, out Guid guid) ? guid : Guid.NewGuid(),
                EnqueuedAt = job.FailedAt,
                Reason = job.Error,
                ExceptionMessage = job.StackTrace,
                ExceptionType = "Exception",
                ContentType = job.Platform,
                Content = data,
                RetryCount = job.RetryCount
            });
        }

        return items;
    }

    /// <inheritdoc />
    public async Task<List<Ghost.Kernel.DeadLetterItem>> DequeueAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        List<DeadLetterItem> items = await PeekAsync(count, cancellationToken).ConfigureAwait(false);

        foreach (DeadLetterItem item in items)
        {
            string? path = FindJobFile(item.Id.ToString("N") ?? string.Empty);
            if (path is not null)
            {
                SafeDelete(path);
            }
        }

        return items;
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectories();
        return await GetQueueDepthAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectories();

        foreach (string path in EnumerateActiveFiles())
        {
            SafeDelete(path);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FailedScrapeJob>> GetFailedJobsAsync(TimeSpan since)
    {
        ValidateLookback(since);
        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        DateTime threshold = GetThresholdUtc(since);
        List<FailedScrapeJob> jobs = [];

        foreach (string path in EnumerateActiveFiles())
        {
            FailedScrapeJob? job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null)
                continue;

            DateTime failedAt = GetFailedAtUtc(job, path);
            if (failedAt >= threshold)
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FailedScrapeJob>> GetFailedJobsByPlatformAsync(string platform, TimeSpan since)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            throw new ArgumentException("Platform must be provided.", nameof(platform));
        }

        ValidateLookback(since);
        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        DateTime threshold = GetThresholdUtc(since);
        List<FailedScrapeJob> jobs = [];

        foreach (string path in EnumerateActiveFiles())
        {
            FailedScrapeJob? job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null)
            {
                continue;
            }

            if (!string.Equals(job.Platform, platform, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DateTime failedAt = GetFailedAtUtc(job, path);
            if (failedAt >= threshold)
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    /// <inheritdoc />
    public async Task<FailedScrapeJob?> GetJobAsync(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("Job id must be provided.", nameof(jobId));
        }

        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        return FindJobFile(jobId) is { } match
            ? await ReadJobAsync(match).ConfigureAwait(false)
            : null;
    }

    /// <inheritdoc />
    public async Task RetryAsync(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("Job id must be provided.", nameof(jobId));
        }

        EnsureDirectories();

        string match = FindJobFile(jobId)
            ?? throw new InvalidOperationException($"Failed job '{jobId}' not found.");
        FailedScrapeJob job = await ReadJobAsync(match).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Failed job '{jobId}' could not be loaded.");

        job.RetryCount++;
        job.LastRetryAt = _timeProvider.GetUtcNow().UtcDateTime;

        await WriteJobAsync(match, job).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RetryAllAsync(TimeSpan since)
    {
        ValidateLookback(since);
        EnsureDirectories();

        DateTime threshold = GetThresholdUtc(since);

        foreach (string path in EnumerateActiveFiles())
        {
            FailedScrapeJob? job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null)
                continue;

            DateTime failedAt = GetFailedAtUtc(job, path);
            if (failedAt < threshold)
                continue;

            job.RetryCount++;
            job.LastRetryAt = _timeProvider.GetUtcNow().UtcDateTime;

            await WriteJobAsync(path, job).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("Job id must be provided.", nameof(jobId));
        }

        EnsureDirectories();

        string archiveMatch = FindJobFile(jobId)
            ?? throw new InvalidOperationException($"Failed job '{jobId}' not found.");
        FailedScrapeJob job = await ReadJobAsync(archiveMatch).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Failed job '{jobId}' could not be loaded.");

        await MoveToArchiveAsync(archiveMatch, job).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ArchiveAllAsync(TimeSpan olderThan)
    {
        ValidateLookback(olderThan);
        EnsureDirectories();

        DateTime threshold = GetThresholdUtc(olderThan);

        foreach (string path in EnumerateActiveFiles())
        {
            FailedScrapeJob? job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null)
                continue;

            DateTime failedAt = GetFailedAtUtc(job, path);
            if (failedAt <= threshold)
                await MoveToArchiveAsync(path, job).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<int> GetQueueDepthAsync()
    {
        EnsureDirectories();
        return Task.FromResult(EnumerateActiveFiles().Count());
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_activePath);
        Directory.CreateDirectory(_archivePath);
    }

    private async Task AutoArchiveIfDueAsync()
    {
        if (_options.AutoArchiveAfter <= TimeSpan.Zero)
        {
            return;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        lock (_archiveGate)
        {
            if (now - _lastArchiveCheckUtc < _options.ArchiveCheckInterval)
            {
                return;
            }

            _lastArchiveCheckUtc = now;
        }

        await ArchiveAllAsync(_options.AutoArchiveAfter).ConfigureAwait(false);
    }

    private IEnumerable<string> EnumerateActiveFiles()
    {
        if (!Directory.Exists(_activePath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateFiles(_activePath, "*.json", SearchOption.TopDirectoryOnly);
    }

    private string GetActiveJobPath(FailedScrapeJob job)
    {
        string platformKey = NormalizePlatformKey(job.Platform);
        string fileName = $"{platformKey}_{job.Id}.json";
        return Path.Combine(_activePath, fileName);
    }

    private static string NormalizePlatformKey(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return "unknown";
        }

        string normalized = new string(platform.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized.Trim('-');
    }

    private static void ValidateLookback(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(span), "Time window must be non-negative.");
        }
    }

    private DateTime GetThresholdUtc(TimeSpan span)
    {
        if (span == TimeSpan.Zero)
        {
            return DateTime.MinValue;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        TimeSpan maxSpan = now - DateTime.MinValue;
        if (span >= maxSpan)
        {
            return DateTime.MinValue;
        }

        return now - span;
    }

    private DateTime GetFailedAtUtc(FailedScrapeJob job, string path)
    {
        if (job.FailedAt != default)
        {
            return DateTime.SpecifyKind(job.FailedAt, DateTimeKind.Utc);
        }

        DateTime lastWrite = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : _timeProvider.GetUtcNow().UtcDateTime;
        job.FailedAt = lastWrite;
        return lastWrite;
    }

    private string? FindJobFile(string jobId)
    {
        foreach (string path in EnumerateActiveFiles())
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            int underscore = fileName.LastIndexOf('_');
            if (underscore <= 0 || underscore >= fileName.Length - 1)
            {
                continue;
            }

            string id = fileName[(underscore + 1)..];
            if (string.Equals(id, jobId, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    private async Task<FailedScrapeJob?> ReadJobAsync(string path)
    {
        try
        {
            string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            FailedScrapeJob? job = JsonSerializer.Deserialize(json, KernelSerializerContext.Default.FailedScrapeJob);
            return job;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
        {
            s_logReadFailed(_logger, path, ex);
            return null;
        }
    }

    private async Task WriteJobAsync(string path, FailedScrapeJob job)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        try
        {
            string json = JsonSerializer.Serialize(job, KernelSerializerContext.Default.FailedScrapeJob);
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);

            File.Move(tempPath, path, true);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            s_logWriteFailed(_logger, path, ex);
            SafeDelete(tempPath);
            throw;
        }
    }

    private async Task MoveToArchiveAsync(string path, FailedScrapeJob job)
    {
        try
        {
            DateTime failedAt = GetFailedAtUtc(job, path);
            string bucket = failedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            string archiveBucketPath = Path.Combine(_archivePath, bucket);
            Directory.CreateDirectory(archiveBucketPath);

            string destination = Path.Combine(archiveBucketPath, Path.GetFileName(path));
            destination = GetUniquePath(destination);

            await Task.Run(() => File.Move(path, destination, true)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            s_logArchiveFailed(_logger, path, ex);
        }
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        for (int i = 1; i <= 1000; i++)
        {
            string candidate = Path.Combine(directory, $"{fileName}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(directory, $"{fileName}_{Guid.NewGuid():N}{extension}");
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
