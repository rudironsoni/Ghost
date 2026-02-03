using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ghost.Core;

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
public sealed class FileSystemDeadLetterQueue : IGenericDeadLetterQueue
{
    private static readonly Action<ILogger, string, Exception?> s_logReadFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(ReadJobAsync)), "Failed to read dead letter job file {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logWriteFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, nameof(WriteJobAsync)), "Failed to write dead letter job file {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logArchiveFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3, nameof(ArchiveAsync)), "Failed to archive dead letter job file {Path}");

    private readonly DeadLetterQueueOptions _options;
    private readonly ILogger<FileSystemDeadLetterQueue> _logger;
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
    public FileSystemDeadLetterQueue(DeadLetterQueueOptions options, ILogger<FileSystemDeadLetterQueue>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rootPath = options.RootPath;
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
    public FileSystemDeadLetterQueue(IOptions<DeadLetterQueueOptions> options, ILogger<FileSystemDeadLetterQueue> logger)
        : this(options?.Value ?? new DeadLetterQueueOptions(), logger)
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
            job.FailedAt = DateTime.UtcNow;
        }

        if (job.Platform is null)
        {
            job.Platform = string.Empty;
        }

        var path = GetActiveJobPath(job);
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
            FailedAt = DateTime.UtcNow,
            StackTrace = exception?.StackTrace ?? string.Empty
        };

        var metadata = new Dictionary<string, object>
        {
            ["Data"] = JsonSerializer.Serialize(item)
        };

        job.Metadata = metadata;

        var path = GetActiveJobPath(job);
        await WriteJobAsync(path, job).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<Ghost.Core.DeadLetterItem>> PeekAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        var items = new List<Ghost.Core.DeadLetterItem>();

        foreach (var path in EnumerateActiveFiles().Take(count))
        {
            var job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null) continue;

            var data = job.Metadata?.GetValueOrDefault("Data")?.ToString() ?? string.Empty;

            items.Add(new Ghost.Core.DeadLetterItem
            {
                Id = Guid.TryParse(job.Id, out var guid) ? guid : Guid.NewGuid(),
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
    public async Task<List<Ghost.Core.DeadLetterItem>> DequeueAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectories();
        await AutoArchiveIfDueAsync().ConfigureAwait(false);

        var items = await PeekAsync(count, cancellationToken).ConfigureAwait(false);

        foreach (var item in items)
        {
            var path = FindJobFile(item.Id.ToString("N") ?? string.Empty);
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

        foreach (var path in EnumerateActiveFiles())
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

        var threshold = GetThresholdUtc(since);
        var jobs = new List<FailedScrapeJob>();

        foreach (var path in EnumerateActiveFiles())
        {
            var job = await ReadJobAsync(path).ConfigureAwait(false);
        if (job is null)
            continue;

            var failedAt = GetFailedAtUtc(job, path);
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

        var threshold = GetThresholdUtc(since);
        var jobs = new List<FailedScrapeJob>();

        foreach (var path in EnumerateActiveFiles())
        {
            var job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null)
            {
                continue;
            }

            if (!string.Equals(job.Platform, platform, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var failedAt = GetFailedAtUtc(job, path);
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

        var match = FindJobFile(jobId)
            ?? throw new InvalidOperationException($"Failed job '{jobId}' not found.");
        var job = await ReadJobAsync(match).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Failed job '{jobId}' could not be loaded.");

        job.RetryCount++;
        job.LastRetryAt = DateTime.UtcNow;

        await WriteJobAsync(match, job).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RetryAllAsync(TimeSpan since)
    {
        ValidateLookback(since);
        EnsureDirectories();

        var threshold = GetThresholdUtc(since);

        foreach (var path in EnumerateActiveFiles())
        {
            var job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null)
                continue;

            var failedAt = GetFailedAtUtc(job, path);
            if (failedAt < threshold)
                continue;

            job.RetryCount++;
            job.LastRetryAt = DateTime.UtcNow;

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

        var archiveMatch = FindJobFile(jobId)
            ?? throw new InvalidOperationException($"Failed job '{jobId}' not found.");
        var job = await ReadJobAsync(archiveMatch).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Failed job '{jobId}' could not be loaded.");

        await MoveToArchiveAsync(archiveMatch, job).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ArchiveAllAsync(TimeSpan olderThan)
    {
        ValidateLookback(olderThan);
        EnsureDirectories();

        var threshold = GetThresholdUtc(olderThan);

        foreach (var path in EnumerateActiveFiles())
        {
            var job = await ReadJobAsync(path).ConfigureAwait(false);
            if (job is null)
                continue;

            var failedAt = GetFailedAtUtc(job, path);
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

        var now = DateTime.UtcNow;
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
        var platformKey = NormalizePlatformKey(job.Platform);
        var fileName = $"{platformKey}_{job.Id}.json";
        return Path.Combine(_activePath, fileName);
    }

    private static string NormalizePlatformKey(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return "unknown";
        }

        var normalized = new string(platform.Trim().ToLowerInvariant()
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

    private static DateTime GetThresholdUtc(TimeSpan span)
    {
        if (span == TimeSpan.Zero)
        {
            return DateTime.MinValue;
        }

        var now = DateTime.UtcNow;
        var maxSpan = now - DateTime.MinValue;
        if (span >= maxSpan)
        {
            return DateTime.MinValue;
        }

        return now - span;
    }

    private static DateTime GetFailedAtUtc(FailedScrapeJob job, string path)
    {
        if (job.FailedAt != default)
        {
            return DateTime.SpecifyKind(job.FailedAt, DateTimeKind.Utc);
        }

        var lastWrite = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.UtcNow;
        job.FailedAt = lastWrite;
        return lastWrite;
    }

    private string? FindJobFile(string jobId)
    {
        foreach (var path in EnumerateActiveFiles())
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var underscore = fileName.LastIndexOf('_');
            if (underscore <= 0 || underscore >= fileName.Length - 1)
            {
                continue;
            }

            var id = fileName[(underscore + 1)..];
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
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var job = await JsonSerializer.DeserializeAsync<FailedScrapeJob>(stream, _serializerOptions).ConfigureAwait(false);
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
        var directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, job, _serializerOptions).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

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
            var failedAt = GetFailedAtUtc(job, path);
            var bucket = failedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var archiveBucketPath = Path.Combine(_archivePath, bucket);
            Directory.CreateDirectory(archiveBucketPath);

            var destination = Path.Combine(archiveBucketPath, Path.GetFileName(path));
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

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 1; i <= 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{fileName}_{i}{extension}");
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
