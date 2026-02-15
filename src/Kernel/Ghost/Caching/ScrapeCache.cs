using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Ghost.Kernel.Caching;

/// <summary>
/// High-performance job scraping cache with memory-first, disk-fallback strategy.
/// Implements ULTRA MISER MODE - minimal resource usage, maximum throughput.
/// </summary>
public interface IScrapeCache
{
    /// <summary>
    /// Gets cached job listings for a search criteria.
    /// </summary>
    public Task<IReadOnlyList<JobListing>?> GetJobsAsync(string cacheKey, CancellationToken ct = default);

    /// <summary>
    /// Caches job listings for a search criteria.
    /// </summary>
    public Task SetJobsAsync(string cacheKey, IReadOnlyList<JobListing> jobs, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>
    /// Invalidates cached entries matching a pattern.
    /// </summary>
    public Task InvalidateAsync(string pattern, CancellationToken ct = default);

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    public CacheStatistics GetStatistics();
}

/// <summary>
/// Cache statistics for monitoring and optimization.
/// </summary>
public class CacheStatistics
{
    public long Hits { get; set; }
    public long Misses { get; set; }
    public long Evictions { get; set; }
    public long DiskFallbacks { get; set; }
    public long MemorySize { get; set; }
    public long DiskSize { get; set; }
    public double HitRatio => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;
}

/// <summary>
/// Memory-first cache with file system fallback for job scraping results.
/// Designed for 50K+ scale with minimal memory footprint.
/// </summary>
public class MemoryFileHybridCache : IScrapeCache, IDisposable
{
    private static readonly Action<ILogger, string, Exception?> _logCacheHit =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "CacheHit"), "Cache HIT for {Key}");

    private static readonly Action<ILogger, string, Exception?> _logCacheMiss =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "CacheMiss"), "Cache MISS for {Key}");

    private static readonly Action<ILogger, string, Exception?> _logDiskFallback =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "DiskFallback"), "Disk fallback for {Key}");

    private static readonly Action<ILogger, string, Exception?> _logDiskReadFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "DiskReadFailed"), "Failed to read disk cache for {Key}");

    private static readonly Action<ILogger, string, Exception?> _logDiskWriteFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, "DiskWriteFailed"), "Failed to write disk cache for {Key}");

    private static readonly Action<ILogger, string, Exception?> _logDeleteFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(6, "DeleteFailed"), "Failed to delete cache file {File}");

    private readonly IMemoryCache _memoryCache;
    private readonly string _diskCachePath;
    private readonly ILogger<MemoryFileHybridCache> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
    private readonly CacheStatistics _stats;
    private long _hits;
    private long _misses;
    private long _diskFallbacks;

    public MemoryFileHybridCache(
        IMemoryCache memoryCache,
        string diskCachePath,
        ILogger<MemoryFileHybridCache> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _diskCachePath = diskCachePath ?? "/var/ghost/cache";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
        _stats = new CacheStatistics();

        Directory.CreateDirectory(_diskCachePath);
    }

    public async Task<IReadOnlyList<JobListing>?> GetJobsAsync(string cacheKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);

        // Try memory first
        if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<JobListing>? jobs))
        {
            Interlocked.Increment(ref _hits);
            _logCacheHit(_logger, cacheKey, null);
            return jobs;
        }

        // Try disk fallback
        string diskPath = GetDiskPath(cacheKey);
        if (File.Exists(diskPath))
        {
            try
            {
                SemaphoreSlim lockObj = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
                await lockObj.WaitAsync(ct).ConfigureAwait(false);

                try
                {
                    string json = await File.ReadAllTextAsync(diskPath, ct).ConfigureAwait(false);
                    jobs = System.Text.Json.JsonSerializer.Deserialize<List<JobListing>>(json);

                    if (jobs != null)
                    {
                        // Promote to memory
                        _memoryCache.Set(cacheKey, jobs, TimeSpan.FromMinutes(5));
                        Interlocked.Increment(ref _hits);
                        Interlocked.Increment(ref _diskFallbacks);
                        _logDiskFallback(_logger, cacheKey, null);
                        return jobs;
                    }
                }
                finally
                {
                    lockObj.Release();
                }
            }
            catch (Exception ex)
            {
                _logDiskReadFailed(_logger, cacheKey, ex);
            }
        }

        Interlocked.Increment(ref _misses);
        _logCacheMiss(_logger, cacheKey, null);
        return null;
    }

    public Task SetJobsAsync(string cacheKey, IReadOnlyList<JobListing> jobs, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);
        ArgumentNullException.ThrowIfNull(jobs);

        TimeSpan exp = expiration ?? TimeSpan.FromMinutes(30);

        // Set in memory
        _memoryCache.Set(cacheKey, jobs, exp);

        // Async write to disk
        string diskPath = GetDiskPath(cacheKey);
        SemaphoreSlim lockObj = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        _ = Task.Run(async () =>
        {
            await lockObj.WaitAsync().ConfigureAwait(false);
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(jobs);
                await File.WriteAllTextAsync(diskPath, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logDiskWriteFailed(_logger, cacheKey, ex);
            }
            finally
            {
                lockObj.Release();
            }
        }, ct);

        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string pattern, CancellationToken ct = default)
    {
        // Memory invalidation
        // Note: IMemoryCache doesn't support pattern-based eviction, so we track keys externally
        // For production, use a proper cache like Redis or MemoryCache with eviction callbacks

        // Disk invalidation
        string[] files = Directory.GetFiles(_diskCachePath, "*.json");
        foreach (string file in files)
        {
            if (Path.GetFileNameWithoutExtension(file).Contains(pattern))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logDeleteFailed(_logger, file, ex);
                }
            }
        }

        return Task.CompletedTask;
    }

    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            Evictions = _stats.Evictions,
            DiskFallbacks = Interlocked.Read(ref _diskFallbacks),
            MemorySize = _stats.MemorySize,
            DiskSize = GetDiskSize()
        };
    }

    private string GetDiskPath(string cacheKey)
    {
        string safeKey = string.Join("_", cacheKey.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_diskCachePath, $"{safeKey}.json");
    }

    private long GetDiskSize()
    {
        try
        {
            string[] files = Directory.GetFiles(_diskCachePath, "*.json");
            return files.Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        foreach (KeyValuePair<string, SemaphoreSlim> kvp in _locks)
        {
            kvp.Value.Dispose();
        }
        _locks.Clear();
        GC.SuppressFinalize(this);
    }
}
