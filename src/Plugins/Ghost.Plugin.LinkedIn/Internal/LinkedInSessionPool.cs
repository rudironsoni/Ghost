using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Plugin.LinkedIn.Internal;

/// <summary>
/// Provides a thread-safe pool of LinkedIn browser sessions.
/// </summary>
public sealed class LinkedInSessionPool : IDisposable
{
    private static readonly Action<ILogger, Exception?> s_logProxyAcquireFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, nameof(CreateSessionOptionsAsync)), "Failed to acquire proxy for session pool");

    private static readonly Action<ILogger, Exception?> s_logDisposeSessionFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, nameof(DisposeSessionAsync)), "Failed to dispose pooled session");
    private readonly IGhostKernel _kernel;
    private readonly LinkedInSessionPoolOptions _options;
    private readonly ILogger<LinkedInSessionPool> _logger;
    private IProxyProvider _proxyProvider;
    private LinkedInOptions _linkedInOptions;

    private readonly ConcurrentQueue<IBrowserSession> _available = new();
    private readonly ConcurrentDictionary<string, IBrowserSession> _inUse = new();
    private readonly ConcurrentDictionary<string, SessionMetadata> _metadata = new();
    private readonly SemaphoreSlim _maxSessions;
    private readonly Timer _healthCheckTimer;

    private long _totalCreated;
    private long _totalRecycled;
    private long _totalDisposed;
    private long _totalAcquisitions;
    private long _totalAcquisitionTimeTicks;
    private long _lastHealthCheckTicks;
    private int _availableCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedInSessionPool"/> class.
    /// </summary>
    public LinkedInSessionPool(
        IGhostKernel kernel,
        LinkedInSessionPoolOptions options,
        ILogger<LinkedInSessionPool> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<LinkedInSessionPool>.Instance;
        _proxyProvider = NullProxyProvider.Instance;
        _linkedInOptions = new LinkedInOptions();

        if (_options.MaxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxSize must be positive.");
        }

        _maxSessions = new SemaphoreSlim(_options.MaxSize, _options.MaxSize);
        _lastHealthCheckTicks = DateTime.UtcNow.Ticks;
        _healthCheckTimer = new Timer(
            _ => _ = Task.Run(() => PruneAsync(CancellationToken.None), CancellationToken.None),
            null,
            _options.HealthCheckInterval,
            _options.HealthCheckInterval);

        // Warmup disabled - causes delays on first request. Sessions created on-demand instead.
    }

    /// <summary>
    /// Static factory method to prevent resolution during DI container validation.
    /// </summary>
    public static LinkedInSessionPool Create(
        IGhostKernel kernel,
        LinkedInSessionPoolOptions options,
        ILogger<LinkedInSessionPool> logger,
        IProxyProvider? proxyProvider,
        LinkedInOptions? linkedInOptions)
    {
        var pool = new LinkedInSessionPool(kernel, options, logger);
        pool._proxyProvider = proxyProvider ?? NullProxyProvider.Instance;
        pool._linkedInOptions = linkedInOptions ?? new LinkedInOptions();
        return pool;
    }



    /// <summary>
    /// Acquire a browser session from the pool.
    /// </summary>
    public async Task<IBrowserSession> AcquireAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sw = Stopwatch.StartNew();
        await _maxSessions.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            while (_available.TryDequeue(out var session))
            {
                Interlocked.Decrement(ref _availableCount);
                if (!TryGetMetadata(session, out var metadata) || metadata is null || !IsReusable(session, metadata))
                {
                    await DisposeSessionAsync(session).ConfigureAwait(false);
                    continue;
                }

                metadata.LastUsedAt = DateTime.UtcNow;
                _inUse[session.SessionId] = session;
                RecordAcquisition(sw);
                return session;
            }

            var created = await CreateSessionAsync(ct).ConfigureAwait(false);
            _inUse[created.SessionId] = created;
            RecordAcquisition(sw);
            return created;
        }
        catch
        {
            _maxSessions.Release();
            throw;
        }
    }

    /// <summary>
    /// Return a session to the pool.
    /// </summary>
    public void Release(IBrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_disposed)
        {
            _ = DisposeSessionAsync(session);
            return;
        }

        if (!_inUse.TryRemove(session.SessionId, out _))
        {
            _ = DisposeSessionAsync(session);
            return;
        }

        if (!TryGetMetadata(session, out var metadata) || metadata is null || metadata.RecycleOnRelease)
        {
            _ = DisposeSessionAsync(session);
            _maxSessions.Release();
            return;
        }

        metadata.LastUsedAt = DateTime.UtcNow;
        if (!IsReusable(session, metadata) || PoolAtCapacity())
        {
            _ = DisposeSessionAsync(session);
            _maxSessions.Release();
            return;
        }

        _available.Enqueue(session);
        Interlocked.Increment(ref _availableCount);
        Interlocked.Increment(ref _totalRecycled);
        _maxSessions.Release();
    }

    /// <summary>
    /// Pre-create sessions in the pool.
    /// </summary>
    public async Task WarmupAsync(int count, CancellationToken ct)
    {
        if (count <= 0)
        {
            return;
        }

        var remaining = _options.MaxSize - (Volatile.Read(ref _availableCount) + _inUse.Count);
        if (remaining <= 0)
        {
            return;
        }

        var target = Math.Min(count, remaining);
        var tasks = new Task[target];
        for (var i = 0; i < target; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                await _maxSessions.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var session = await CreateSessionAsync(ct).ConfigureAwait(false);
                    if (PoolAtCapacity())
                    {
                        await DisposeSessionAsync(session).ConfigureAwait(false);
                        return;
                    }

                    _available.Enqueue(session);
                    Interlocked.Increment(ref _availableCount);
                }
                finally
                {
                    _maxSessions.Release();
                }
            }, ct);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Remove stale sessions based on idle time, lifetime, or connectivity.
    /// </summary>
    public async Task PruneAsync(CancellationToken ct)
    {
        if (_disposed)
        {
            return;
        }

        Interlocked.Exchange(ref _lastHealthCheckTicks, DateTime.UtcNow.Ticks);

        var keepQueue = new ConcurrentQueue<IBrowserSession>();
        while (_available.TryDequeue(out var session))
        {
            Interlocked.Decrement(ref _availableCount);
            if (!TryGetMetadata(session, out var metadata) || !IsReusable(session, metadata))
            {
                await DisposeSessionAsync(session).ConfigureAwait(false);
            }
            else
            {
                keepQueue.Enqueue(session);
                Interlocked.Increment(ref _availableCount);
            }
        }

        while (keepQueue.TryDequeue(out var session))
        {
            _available.Enqueue(session);
        }

        foreach (var kvp in _inUse)
        {
            ct.ThrowIfCancellationRequested();
            if (TryGetMetadata(kvp.Value, out var metadata))
            {
                var session = kvp.Value;
                if (IsExpired(metadata) || !session.IsConnected)
                {
                    metadata.RecycleOnRelease = true;
                }
            }
        }
    }

    /// <summary>
    /// Get pool metrics snapshot.
    /// </summary>
    public SessionPoolMetrics GetMetrics()
    {
        var totalAcquisitions = Interlocked.Read(ref _totalAcquisitions);
        var avgTicks = totalAcquisitions > 0
            ? TimeSpan.FromTicks(Interlocked.Read(ref _totalAcquisitionTimeTicks) / totalAcquisitions)
            : TimeSpan.Zero;

        return new SessionPoolMetrics
        {
            AvailableCount = Math.Max(0, Volatile.Read(ref _availableCount)),
            InUseCount = _inUse.Count,
            TotalCreated = (int)Interlocked.Read(ref _totalCreated),
            TotalRecycled = (int)Interlocked.Read(ref _totalRecycled),
            TotalDisposed = (int)Interlocked.Read(ref _totalDisposed),
            AverageAcquisitionTime = avgTicks,
            LastHealthCheck = new DateTime(Interlocked.Read(ref _lastHealthCheckTicks), DateTimeKind.Utc)
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _healthCheckTimer.Dispose();
        _maxSessions.Dispose();

        while (_available.TryDequeue(out var session))
        {
            _ = DisposeSessionAsync(session);
        }

        foreach (var session in _inUse.Values)
        {
            _ = DisposeSessionAsync(session);
        }

        _inUse.Clear();
        _metadata.Clear();
    }


    private void RecordAcquisition(Stopwatch sw)
    {
        sw.Stop();
        Interlocked.Increment(ref _totalAcquisitions);
        Interlocked.Add(ref _totalAcquisitionTimeTicks, sw.Elapsed.Ticks);
    }

    private bool IsReusable(IBrowserSession session, SessionMetadata metadata)
    {
        return !metadata.RecycleOnRelease && !IsExpired(metadata) && session.IsConnected;
    }

    private bool IsExpired(SessionMetadata metadata)
    {
        var now = DateTime.UtcNow;
        var idleExpired = now - metadata.LastUsedAt > _options.MaxIdleTime;
        var lifetimeExpired = now - metadata.CreatedAt > _options.MaxLifetime;
        return idleExpired || lifetimeExpired;
    }

    private async Task<IBrowserSession> CreateSessionAsync(CancellationToken ct)
    {
        try
        {
            var sessionOptions = await CreateSessionOptionsAsync(ct).ConfigureAwait(false);
            var session = await _kernel.NewSessionAsync(sessionOptions, ct).ConfigureAwait(false);
            _metadata[session.SessionId] = new SessionMetadata(DateTime.UtcNow);
            Interlocked.Increment(ref _totalCreated);
            return session;
        }
        catch (Exception ex) when (ex is Microsoft.Playwright.PlaywrightException ||
                                    ex.Message.Contains("TargetClosedException", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("ERR_SOCKS_CONNECTION_FAILED", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("Process exited", StringComparison.OrdinalIgnoreCase))
        {
            throw new BrowserServiceUnavailableException(
                "Failed to initialize browser session. Browser automation service may be unavailable or proxy connection failed.",
                ex);
        }
    }

    private async Task<SessionOptions> CreateSessionOptionsAsync(CancellationToken ct)
    {
        var options = new SessionOptions
        {
            StorageStatePath = _linkedInOptions.StorageStatePath,
            TimezoneId = _linkedInOptions.TimezoneId,
            Locale = _linkedInOptions.Locale
        };

        if (_linkedInOptions.ProxyEnabled)
        {
            try
            {
                var proxy = await _proxyProvider.GetProxyAsync("US", ct).ConfigureAwait(false);
                if (proxy is not null)
                {
                    options.Proxy = new SessionOptions.ProxySettings(proxy.Server, proxy.Username, proxy.Password);
                }
            }
            catch (Exception ex)
            {
                s_logProxyAcquireFailed(_logger, ex);
            }
        }

        return options;
    }

    private async Task DisposeSessionAsync(IBrowserSession session)
    {
        try
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            s_logDisposeSessionFailed(_logger, ex);
        }
        finally
        {
            _metadata.TryRemove(session.SessionId, out _);
            Interlocked.Increment(ref _totalDisposed);
        }
    }

    private bool PoolAtCapacity()
    {
        var available = Volatile.Read(ref _availableCount);
        return available + _inUse.Count >= _options.MaxSize;
    }

    private bool TryGetMetadata(IBrowserSession session, out SessionMetadata metadata)
    {
        if (_metadata.TryGetValue(session.SessionId, out var existing) && existing is not null)
        {
            metadata = existing;
            return true;
        }

        metadata = null!;
        return false;
    }

    private sealed class SessionMetadata
    {
        public SessionMetadata(DateTime createdAt)
        {
            CreatedAt = createdAt;
            LastUsedAt = createdAt;
        }

        public DateTime CreatedAt { get; }
        public DateTime LastUsedAt { get; set; }
        public bool RecycleOnRelease { get; set; }
    }

    private sealed class NullProxyProvider : IProxyProvider
    {
        public static NullProxyProvider Instance { get; } = new();

        public Task<ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken token = default)
        {
            return Task.FromResult<ProxyInfo?>(null);
        }
    }
}
