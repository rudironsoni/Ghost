using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ghost.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Pool;

[SuppressMessage("Performance", "CA1848:Use LoggerMessage delegates", Justification = "Pool infrastructure - readability over performance")]
public sealed class TieredBrowserPool : ITieredBrowserPool
{
    private readonly GhostKernel _kernel;
    private readonly TieredBrowserPoolOptions _options;
    private readonly ILogger<TieredBrowserPool> _logger;
    
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
        ILogger<TieredBrowserPool>? logger = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _options = options ?? new TieredBrowserPoolOptions();
        _logger = logger ?? NullLogger<TieredBrowserPool>.Instance;
        
        _coldPoolSemaphore = new SemaphoreSlim(_options.Cold.MaximumConcurrent, _options.Cold.MaximumConcurrent);
        
        _healthCheckTimer = new Timer(
            HealthCheckCallback,
            null,
            _options.HealthCheckInterval,
            _options.HealthCheckInterval);
        
        _ = Task.Run(async () => await InitializePoolsAsync());
    }

    private async Task InitializePoolsAsync()
    {
        try
        {
            await WarmUpAsync(Tier.Hot, _options.Hot.MinimumSize, CancellationToken.None);
            await WarmUpAsync(Tier.Warm, _options.Warm.MinimumSize, CancellationToken.None);
            
            _logger.LogInformation(
                "Tiered browser pool initialized: Hot={HotCount}, Warm={WarmCount}",
                _options.Hot.MinimumSize,
                _options.Warm.MinimumSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize browser pools");
        }
    }

    public async Task<IBrowserSession> AcquireBrowserAsync(Tier tier = Tier.Hot, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _totalAcquisitions);
        var sw = Stopwatch.StartNew();

        try
        {
            var session = tier switch
            {
                Tier.Hot => await AcquireFromHotPoolAsync(ct),
                Tier.Warm => await AcquireFromWarmPoolAsync(ct),
                Tier.Cold => await AcquireFromColdPoolAsync(ct),
                _ => throw new ArgumentOutOfRangeException(nameof(tier))
            };

            sw.Stop();
            RecordAcquisitionTime(tier, sw.Elapsed.TotalMilliseconds);
            
            _activeSessions[session.SessionId] = new PooledBrowserSession
            {
                Session = session,
                Tier = tier,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                IsAvailable = false,
                UseCount = 1
            };

            _logger.LogDebug(
                "Acquired browser from {Tier} pool in {ElapsedMs}ms (SessionId={SessionId})",
                tier,
                sw.Elapsed.TotalMilliseconds,
                session.SessionId);

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
        if (_hotPool.TryTake(out var pooled) && !pooled.IsExpired(_options.Hot.MaxAge))
        {
            Interlocked.Increment(ref _hotAcquisitions);
            pooled.LastUsedAt = DateTime.UtcNow;
            pooled.UseCount++;
            
            _ = Task.Run(async () => await ReplenishHotPoolAsync(), CancellationToken.None);
            
            return pooled.Session;
        }

        pooled?.Dispose();

        return await AcquireFromWarmPoolAsync(ct);
    }

    private async Task<IBrowserSession> AcquireFromWarmPoolAsync(CancellationToken ct)
    {
        if (_warmPool.TryTake(out var pooled))
        {
            Interlocked.Increment(ref _warmAcquisitions);
            
            if (pooled.IsExpired(_options.Warm.MaxAge))
            {
                pooled.Dispose();
                return await CreateNewSessionAsync(ct);
            }
            
            pooled.LastUsedAt = DateTime.UtcNow;
            pooled.UseCount++;
            
            _ = Task.Run(async () => await ReplenishWarmPoolAsync(), CancellationToken.None);
            
            return pooled.Session;
        }

        return await AcquireFromColdPoolAsync(ct);
    }

    private async Task<IBrowserSession> AcquireFromColdPoolAsync(CancellationToken ct)
    {
        await _coldPoolSemaphore.WaitAsync(ct);
        
        try
        {
            Interlocked.Increment(ref _coldAcquisitions);
            return await CreateNewSessionAsync(ct);
        }
        catch
        {
            _coldPoolSemaphore.Release();
            throw;
        }
    }

    private async Task<IBrowserSession> CreateNewSessionAsync(CancellationToken ct)
    {
        var memoryPressure = GetMemoryPressure();
        
        if (memoryPressure > _options.MemoryPressureThreshold)
        {
            _logger.LogWarning(
                "High memory pressure: {Pressure:P0}, triggering cleanup",
                memoryPressure);
            
            await CleanupExpiredSessionsAsync();
        }

        return await _kernel.NewSessionAsync(null, ct);
    }

    public async Task ReturnBrowserAsync(IBrowserSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_activeSessions.TryRemove(session.SessionId, out var pooled))
        {
            await session.DisposeAsync();
            return;
        }

        try
        {
            if (pooled.IsExpired(_options.SessionTtl))
            {
                _logger.LogDebug("Session expired, disposing (SessionId={SessionId})", session.SessionId);
                await session.DisposeAsync();
                return;
            }

            pooled.IsAvailable = true;
            pooled.LastUsedAt = DateTime.UtcNow;

            var targetPool = pooled.Tier switch
            {
                Tier.Hot => _hotPool,
                Tier.Warm => _warmPool,
                _ => null
            };

            if (targetPool != null)
            {
                var currentCount = targetPool.Count;
                var maxSize = pooled.Tier == Tier.Hot ? _options.Hot.MaximumSize : _options.Warm.MaximumSize;

                if (currentCount < maxSize)
                {
                    targetPool.Add(pooled);
                    _logger.LogDebug(
                        "Returned session to {Tier} pool (SessionId={SessionId})",
                        pooled.Tier,
                        session.SessionId);
                    return;
                }
            }

            if (pooled.Tier == Tier.Cold)
            {
                _coldPoolSemaphore.Release();
            }

            await session.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning session to pool");
            await session.DisposeAsync();
        }
    }

    public Task<PoolHealth> GetHealthAsync(CancellationToken ct = default)
    {
        var hotSessions = _hotPool.ToArray();
        var warmSessions = _warmPool.ToArray();
        var activeSessions = _activeSessions.Count;

        var hotAvg = _hotAcquisitions > 0 ? _hotAcquisitionTimeSum / _hotAcquisitions : 0;
        var warmAvg = _warmAcquisitions > 0 ? _warmAcquisitionTimeSum / _warmAcquisitions : 0;
        var coldAvg = _coldAcquisitions > 0 ? _coldAcquisitionTimeSum / _coldAcquisitions : 0;

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

        var memoryPressure = GetMemoryPressure();

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
                    var session = await CreateNewSessionAsync(ct);
                    var pooled = new PooledBrowserSession
                    {
                        Session = session,
                        Tier = tier,
                        CreatedAt = DateTime.UtcNow,
                        LastUsedAt = DateTime.UtcNow,
                        IsAvailable = true,
                        UseCount = 0
                    };

                    var targetPool = tier switch
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
                        await session.DisposeAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to warm up {Tier} pool", tier);
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ReplenishHotPoolAsync()
    {
        await _hotPoolLock.WaitAsync();
        try
        {
            var currentCount = _hotPool.Count;
            if (currentCount < _options.Hot.MinimumSize)
            {
                var needed = _options.Hot.MinimumSize - currentCount;
                await WarmUpAsync(Tier.Hot, needed, CancellationToken.None);
            }
        }
        finally
        {
            _hotPoolLock.Release();
        }
    }

    private async Task ReplenishWarmPoolAsync()
    {
        await _warmPoolLock.WaitAsync();
        try
        {
            var currentCount = _warmPool.Count;
            if (currentCount < _options.Warm.MinimumSize)
            {
                var needed = _options.Warm.MinimumSize - currentCount;
                await WarmUpAsync(Tier.Warm, needed, CancellationToken.None);
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
                await CleanupExpiredSessionsAsync();
                
                var health = await GetHealthAsync();
                
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
                    await ReplenishHotPoolAsync();
                }

                if (health.Warm.Total < _options.Warm.MinimumSize)
                {
                    await ReplenishWarmPoolAsync();
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
        var hotExpired = new List<PooledBrowserSession>();
        var warmExpired = new List<PooledBrowserSession>();

        foreach (var session in _hotPool)
        {
            if (session.IsExpired(_options.Hot.MaxAge))
            {
                hotExpired.Add(session);
            }
        }

        foreach (var session in _warmPool)
        {
            if (session.IsExpired(_options.Warm.MaxAge))
            {
                warmExpired.Add(session);
            }
        }

        foreach (var session in hotExpired)
        {
            if (_hotPool.TryTake(out _))
            {
                session.Dispose();
            }
        }

        foreach (var session in warmExpired)
        {
            if (_warmPool.TryTake(out _))
            {
                session.Dispose();
            }
        }

        if (hotExpired.Count > 0 || warmExpired.Count > 0)
        {
            _logger.LogInformation(
                "Cleaned up expired sessions: Hot={HotExpired}, Warm={WarmExpired}",
                hotExpired.Count,
                warmExpired.Count);
        }

        return Task.CompletedTask;
    }

    private static double GetMemoryPressure()
    {
        var gcInfo = GC.GetGCMemoryInfo();
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

        await _healthCheckTimer.DisposeAsync();

        var allSessions = _hotPool.Concat(_warmPool).Concat(_activeSessions.Values).ToList();

        foreach (var session in allSessions)
        {
            try
            {
                await session.Session.DisposeAsync();
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
