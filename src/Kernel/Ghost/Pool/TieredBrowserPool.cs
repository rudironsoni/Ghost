using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ghost.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Pool;

[SuppressMessage("Performance", "CA1848:Use LoggerMessage delegates", Justification = "Pool infrastructure - readability over performance")]
public sealed class TieredBrowserPool : ITieredBrowserPool
{
    private readonly GhostKernel _kernel;
    private readonly TieredBrowserPoolOptions _options;
    private readonly ILogger<TieredBrowserPool> _logger;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentBag<PooledBrowserSession> _hotPool = new();
    private readonly ConcurrentBag<PooledBrowserSession> _warmPool = new();
    private readonly SemaphoreSlim _coldPoolSemaphore;

    private readonly SemaphoreSlim _hotPoolLock = new(1, 1);
    private readonly SemaphoreSlim _warmPoolLock = new(1, 1);

    private readonly ConcurrentDictionary<string, PooledBrowserSession> _activeSessions = new();

    private long _totalAcquisitions;
    private long _hotAcquisitions;
    private long _warmAcquisitions;
    private long _coldAcquisitions;

    private double _hotAcquisitionTimeSum;
    private double _warmAcquisitionTimeSum;
    private double _coldAcquisitionTimeSum;

    private readonly Timer _healthCheckTimer;
    private bool _disposed;

    public TieredBrowserPool(
        GhostKernel kernel,
        TieredBrowserPoolOptions? options = null,
        ILogger<TieredBrowserPool>? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        _kernel = kernel;
        _options = options ?? new TieredBrowserPoolOptions();
        _logger = logger ?? NullLogger<TieredBrowserPool>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;

        _coldPoolSemaphore = new SemaphoreSlim(_options.Cold.MaximumConcurrent, _options.Cold.MaximumConcurrent);

        _healthCheckTimer = new Timer(
            HealthCheckCallback,
            null,
            _options.HealthCheckInterval,
            _options.HealthCheckInterval);

        _ = Task.Run(async () => await InitializePoolsAsync().ConfigureAwait(false));
    }

    private async Task InitializePoolsAsync()
    {
        try
        {
            await WarmUpAsync(Tier.Hot, _options.Hot.MinimumSize, CancellationToken.None).ConfigureAwait(false);
            await WarmUpAsync(Tier.Warm, _options.Warm.MinimumSize, CancellationToken.None).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Tiered browser pool initialized: Hot={HotCount}, Warm={WarmCount}",
                    _options.Hot.MinimumSize,
                    _options.Warm.MinimumSize);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize browser pools");
        }
    }

    public async Task<IBrowserSession> AcquireBrowserAsync(Tier tier = Tier.Hot, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _totalAcquisitions);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            IBrowserSession session = tier switch
            {
                Tier.Hot => await AcquireFromHotPoolAsync(ct).ConfigureAwait(false),
                Tier.Warm => await AcquireFromWarmPoolAsync(ct).ConfigureAwait(false),
                Tier.Cold => await AcquireFromColdPoolAsync(ct).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(tier))
            };

            stopwatch.Stop();
            RecordAcquisitionTime(tier, stopwatch.Elapsed.TotalMilliseconds);

            _activeSessions[session.SessionId] = new PooledBrowserSession
            {
                Session = session,
                Tier = tier,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                LastUsedAt = _timeProvider.GetUtcNow().UtcDateTime,
                IsAvailable = false,
                UseCount = 1
            };

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Acquired browser from {Tier} pool in {ElapsedMs}ms (SessionId={SessionId})",
                    tier,
                    stopwatch.Elapsed.TotalMilliseconds,
                    session.SessionId);
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire browser from {Tier} pool", tier);
            throw;
        }
    }

    private async Task<IBrowserSession> AcquireFromHotPoolAsync(CancellationToken ct)
    {
        if (_hotPool.TryTake(out PooledBrowserSession? pooled) && !pooled.IsExpired(_options.Hot.MaxAge))
        {
            Interlocked.Increment(ref _hotAcquisitions);
            pooled.LastUsedAt = _timeProvider.GetUtcNow().UtcDateTime;
            pooled.UseCount++;

            _ = Task.Run(async () => await ReplenishHotPoolAsync().ConfigureAwait(false), CancellationToken.None);

            return pooled.Session;
        }

        pooled?.Dispose();

        return await AcquireFromWarmPoolAsync(ct).ConfigureAwait(false);
    }

    private async Task<IBrowserSession> AcquireFromWarmPoolAsync(CancellationToken ct)
    {
        if (_warmPool.TryTake(out PooledBrowserSession? pooled))
        {
            Interlocked.Increment(ref _warmAcquisitions);

            if (pooled.IsExpired(_options.Warm.MaxAge))
            {
                pooled.Dispose();
                return await CreateNewSessionAsync(ct).ConfigureAwait(false);
            }

            pooled.LastUsedAt = _timeProvider.GetUtcNow().UtcDateTime;
            pooled.UseCount++;

            _ = Task.Run(async () => await ReplenishWarmPoolAsync().ConfigureAwait(false), CancellationToken.None);

            return pooled.Session;
        }

        return await AcquireFromColdPoolAsync(ct).ConfigureAwait(false);
    }

    private async Task<IBrowserSession> AcquireFromColdPoolAsync(CancellationToken ct)
    {
        await _coldPoolSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            Interlocked.Increment(ref _coldAcquisitions);
            return await CreateNewSessionAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            _coldPoolSemaphore.Release();
            throw;
        }
    }

    private async Task<IBrowserSession> CreateNewSessionAsync(CancellationToken ct)
    {
        double memoryPressure = GetMemoryPressure();

        if (memoryPressure > _options.MemoryPressureThreshold)
        {
            _logger.LogWarning(
                "High memory pressure: {Pressure:P0}, triggering cleanup",
                memoryPressure);

            await CleanupExpiredSessionsAsync().ConfigureAwait(false);
        }

        return await _kernel.NewSessionAsync(null, ct).ConfigureAwait(false);
    }

    public async Task ReturnBrowserAsync(IBrowserSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_activeSessions.TryRemove(session.SessionId, out PooledBrowserSession? pooled))
        {
            await session.DisposeAsync().ConfigureAwait(false);
            return;
        }

        try
        {
            if (pooled.IsExpired(_options.SessionTtl))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Session expired, disposing (SessionId={SessionId})", session.SessionId);
                }
                await session.DisposeAsync().ConfigureAwait(false);
                return;
            }

            pooled.IsAvailable = true;
            pooled.LastUsedAt = _timeProvider.GetUtcNow().UtcDateTime;

            ConcurrentBag<PooledBrowserSession>? targetPool = pooled.Tier switch
            {
                Tier.Hot => _hotPool,
                Tier.Warm => _warmPool,
                _ => null
            };

            if (targetPool != null)
            {
                int currentCount = targetPool.Count;
                int maxSize = pooled.Tier == Tier.Hot ? _options.Hot.MaximumSize : _options.Warm.MaximumSize;

                if (currentCount < maxSize)
                {
                    targetPool.Add(pooled);
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Returned session to {Tier} pool (SessionId={SessionId})",
                            pooled.Tier,
                            session.SessionId);
                    }
                    return;
                }
            }

            if (pooled.Tier == Tier.Cold)
            {
                _coldPoolSemaphore.Release();
            }

            await session.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning session to pool");
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    public Task<PoolHealth> GetHealthAsync(CancellationToken ct = default)
    {
        PooledBrowserSession[] hotSessions = _hotPool.ToArray();
        PooledBrowserSession[] warmSessions = _warmPool.ToArray();
        int activeSessions = _activeSessions.Count;

        double hotAvg = _hotAcquisitions > 0 ? _hotAcquisitionTimeSum / _hotAcquisitions : 0;
        double warmAvg = _warmAcquisitions > 0 ? _warmAcquisitionTimeSum / _warmAcquisitions : 0;
        double coldAvg = _coldAcquisitions > 0 ? _coldAcquisitionTimeSum / _coldAcquisitions : 0;

        var hotHealth = new TierHealth
        {
            Available = hotSessions.Count(s => s.IsAvailable),
            InUse = hotSessions.Count(s => !s.IsAvailable),
            Total = hotSessions.Length,
            AverageAcquisitionTimeMs = hotAvg,
            AcquisitionCount = _hotAcquisitions,
            IsHealthy = hotSessions.Length >= _options.Hot.MinimumSize && hotAvg < 500
        };

        var warmHealth = new TierHealth
        {
            Available = warmSessions.Count(s => s.IsAvailable),
            InUse = warmSessions.Count(s => !s.IsAvailable),
            Total = warmSessions.Length,
            AverageAcquisitionTimeMs = warmAvg,
            AcquisitionCount = _warmAcquisitions,
            IsHealthy = warmSessions.Length >= _options.Warm.MinimumSize && warmAvg < 1500
        };

        var coldHealth = new TierHealth
        {
            Available = _coldPoolSemaphore.CurrentCount,
            InUse = _options.Cold.MaximumConcurrent - _coldPoolSemaphore.CurrentCount,
            Total = _options.Cold.MaximumConcurrent,
            AverageAcquisitionTimeMs = coldAvg,
            AcquisitionCount = _coldAcquisitions,
            IsHealthy = _coldPoolSemaphore.CurrentCount > 0
        };

        double memoryPressure = GetMemoryPressure();

        return Task.FromResult(new PoolHealth
        {
            Hot = hotHealth,
            Warm = warmHealth,
            Cold = coldHealth,
            IsHealthy = hotHealth.IsHealthy && warmHealth.IsHealthy && coldHealth.IsHealthy && memoryPressure < _options.MemoryPressureThreshold,
            TotalAcquisitions = _totalAcquisitions,
            ActiveSessions = activeSessions,
            MemoryPressure = memoryPressure
        });
    }

    public async Task WarmUpAsync(Tier tier, int count, CancellationToken ct = default)
    {
        var tasks = new List<Task>(count);

        for (int i = 0; i < count; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    IBrowserSession session = await CreateNewSessionAsync(ct).ConfigureAwait(false);
                    var pooled = new PooledBrowserSession
                    {
                        Session = session,
                        Tier = tier,
                        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                        LastUsedAt = _timeProvider.GetUtcNow().UtcDateTime,
                        IsAvailable = true,
                        UseCount = 0
                    };

                    ConcurrentBag<PooledBrowserSession>? targetPool = tier switch
                    {
                        Tier.Hot => _hotPool,
                        Tier.Warm => _warmPool,
                        _ => null
                    };

                    if (targetPool != null)
                    {
                        targetPool.Add(pooled);
                    }
                    else
                    {
                        await session.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to warm up {Tier} pool", tier);
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task ReplenishHotPoolAsync()
    {
        await _hotPoolLock.WaitAsync().ConfigureAwait(false);
        try
        {
            int currentCount = _hotPool.Count;
            if (currentCount < _options.Hot.MinimumSize)
            {
                int needed = _options.Hot.MinimumSize - currentCount;
                await WarmUpAsync(Tier.Hot, needed, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _hotPoolLock.Release();
        }
    }

    private async Task ReplenishWarmPoolAsync()
    {
        await _warmPoolLock.WaitAsync().ConfigureAwait(false);
        try
        {
            int currentCount = _warmPool.Count;
            if (currentCount < _options.Warm.MinimumSize)
            {
                int needed = _options.Warm.MinimumSize - currentCount;
                await WarmUpAsync(Tier.Warm, needed, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _warmPoolLock.Release();
        }
    }

    private void HealthCheckCallback(object? state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await CleanupExpiredSessionsAsync().ConfigureAwait(false);

                PoolHealth health = await GetHealthAsync().ConfigureAwait(false);

                if (!health.IsHealthy)
                {
                    _logger.LogWarning(
                        "Pool health degraded: Hot={HotHealthy}, Warm={WarmHealthy}, Cold={ColdHealthy}, Memory={MemoryPressure:P0}",
                        health.Hot.IsHealthy,
                        health.Warm.IsHealthy,
                        health.Cold.IsHealthy,
                        health.MemoryPressure);
                }

                if (health.Hot.Total < _options.Hot.MinimumSize)
                {
                    await ReplenishHotPoolAsync().ConfigureAwait(false);
                }

                if (health.Warm.Total < _options.Warm.MinimumSize)
                {
                    await ReplenishWarmPoolAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
            }
        });
    }

    private Task CleanupExpiredSessionsAsync()
    {
        List<PooledBrowserSession> hotExpired = [];
        List<PooledBrowserSession> warmExpired = [];

        foreach (PooledBrowserSession session in _hotPool)
        {
            if (session.IsExpired(_options.Hot.MaxAge))
            {
                hotExpired.Add(session);
            }
        }

        foreach (PooledBrowserSession session in _warmPool)
        {
            if (session.IsExpired(_options.Warm.MaxAge))
            {
                warmExpired.Add(session);
            }
        }

        foreach (PooledBrowserSession session in hotExpired)
        {
            if (_hotPool.TryTake(out _))
            {
                session.Dispose();
            }
        }

        foreach (PooledBrowserSession session in warmExpired)
        {
            if (_warmPool.TryTake(out _))
            {
                session.Dispose();
            }
        }

        if (hotExpired.Count > 0 || warmExpired.Count > 0)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Cleaned up expired sessions: Hot={HotExpired}, Warm={WarmExpired}",
                    hotExpired.Count,
                    warmExpired.Count);
            }
        }

        return Task.CompletedTask;
    }

    private static double GetMemoryPressure()
    {
        GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();
        return (double)gcInfo.HeapSizeBytes / gcInfo.TotalAvailableMemoryBytes;
    }

    private void RecordAcquisitionTime(Tier tier, double milliseconds)
    {
        switch (tier)
        {
            case Tier.Hot:
                _hotAcquisitionTimeSum += milliseconds;
                break;
            case Tier.Warm:
                _warmAcquisitionTimeSum += milliseconds;
                break;
            case Tier.Cold:
                _coldAcquisitionTimeSum += milliseconds;
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _healthCheckTimer.DisposeAsync().ConfigureAwait(false);

        var allSessions = _hotPool.Concat(_warmPool).Concat(_activeSessions.Values).ToList();

        foreach (PooledBrowserSession? session in allSessions)
        {
            try
            {
                await session.Session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing session");
            }
        }

        _hotPool.Clear();
        _warmPool.Clear();
        _activeSessions.Clear();

        _hotPoolLock.Dispose();
        _warmPoolLock.Dispose();
        _coldPoolSemaphore.Dispose();

        _logger.LogInformation("Tiered browser pool disposed");
    }
}
