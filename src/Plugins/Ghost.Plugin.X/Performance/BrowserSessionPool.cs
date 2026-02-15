using System.Collections.Concurrent;
using Ghost.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.X.Performance;

/// <summary>
/// Pool of browser sessions for efficient reuse.
/// </summary>
public interface IBrowserSessionPool : IDisposable
{
    /// <summary>
    /// Gets a session from the pool or creates a new one.
    /// </summary>
    public Task<IBrowserSession> GetSessionAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a session to the pool.
    /// </summary>
    public void ReturnSession(IBrowserSession session);

    /// <summary>
    /// Gets the number of available sessions in the pool.
    /// </summary>
    public int AvailableCount { get; }

    /// <summary>
    /// Gets the total number of sessions (in use + available).
    /// </summary>
    public int TotalCount { get; }
}

/// <summary>
/// Configuration for browser session pool.
/// </summary>
public class BrowserSessionPoolOptions
{
    /// <summary>
    /// Maximum number of sessions in the pool.
    /// </summary>
    public int MaxPoolSize { get; set; } = 5;

    /// <summary>
    /// Minimum number of sessions to keep in the pool.
    /// </summary>
    public int MinPoolSize { get; set; } = 1;

    /// <summary>
    /// Maximum time a session can be idle before being closed.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Maximum time to wait for a session from the pool.
    /// </summary>
    public TimeSpan AcquireTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Implementation of browser session pool.
/// </summary>
public partial class BrowserSessionPool : IBrowserSessionPool
{
    private readonly IGhostKernel _kernel;
    private readonly BrowserSessionPoolOptions _options;
    private readonly ILogger<BrowserSessionPool> _logger;
    private readonly ConcurrentBag<PoolEntry> _availableSessions = new();
    private readonly ConcurrentDictionary<IBrowserSession, PoolEntry> _inUseSessions = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public BrowserSessionPool(
        IGhostKernel kernel,
        BrowserSessionPoolOptions options,
        ILogger<BrowserSessionPool> logger)
    {
        _kernel = kernel;
        _options = options ?? new BrowserSessionPoolOptions();
        _logger = logger;
        _semaphore = new SemaphoreSlim(_options.MaxPoolSize);
        _cleanupTimer = new Timer(CleanupIdleSessions, null, _options.IdleTimeout, _options.IdleTimeout);

        Log.PoolInitialized(_logger, _options.MaxPoolSize);
    }

    public int AvailableCount => _availableSessions.Count;
    public int TotalCount => _availableSessions.Count + _inUseSessions.Count;

    public async Task<IBrowserSession> GetSessionAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try to acquire semaphore slot
        if (!await _semaphore.WaitAsync(_options.AcquireTimeout, ct).ConfigureAwait(false))
        {
            throw new TimeoutException("Could not acquire browser session from pool within timeout");
        }

        try
        {
            // Try to get an existing session
            if (_availableSessions.TryTake(out PoolEntry? entry))
            {
                entry.LastUsedAt = DateTime.UtcNow;
                _inUseSessions[entry.Session] = entry;
                Log.SessionReused(_logger, AvailableCount, _inUseSessions.Count);
                return entry.Session;
            }

            // Create new session
            IBrowserSession session = await _kernel.NewSessionAsync(null, ct).ConfigureAwait(false);
            entry = new PoolEntry
            {
                Session = session,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            };
            _inUseSessions[session] = entry;
            Log.SessionCreated(_logger, TotalCount);
            return session;
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    public void ReturnSession(IBrowserSession session)
    {
        if (_disposed || session == null)
        {
            return;
        }

        if (_inUseSessions.TryRemove(session, out PoolEntry? entry))
        {
            entry.LastUsedAt = DateTime.UtcNow;

            // Check if we should return to pool or dispose
            if (_availableSessions.Count < _options.MaxPoolSize && !IsSessionExpired(entry))
            {
                _availableSessions.Add(entry);
                Log.SessionReturned(_logger, AvailableCount, _inUseSessions.Count);
            }
            else
            {
                // Dispose the session
                _ = DisposeSessionAsync(entry);
            }
        }

        _semaphore.Release();
    }

    private bool IsSessionExpired(PoolEntry entry)
    {
        TimeSpan idleTime = DateTime.UtcNow - entry.LastUsedAt;
        return idleTime > _options.IdleTimeout;
    }

    private async void CleanupIdleSessions(object? state)
    {
        try
        {
            var expiredSessions = _availableSessions.Where(IsSessionExpired).ToList();

            foreach (PoolEntry? entry in expiredSessions)
            {
                if (_availableSessions.TryTake(out PoolEntry? removed) && removed == entry)
                {
                    await DisposeSessionAsync(entry).ConfigureAwait(false);
                    Log.IdleSessionCleaned(_logger);
                }
            }
        }
        catch (Exception ex)
        {
            Log.CleanupError(_logger, ex);
        }
    }

    private async Task DisposeSessionAsync(PoolEntry entry)
    {
        try
        {
            await entry.Session.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.DisposeSessionError(_logger, ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cleanupTimer?.Dispose();
        _semaphore?.Dispose();

        // Dispose all sessions
        var allSessions = _availableSessions.ToList().Concat(_inUseSessions.Values).ToList();
        foreach (PoolEntry? entry in allSessions)
        {
            _ = DisposeSessionAsync(entry);
        }

        Log.PoolDisposed(_logger);

        GC.SuppressFinalize(this);
    }

    private sealed class PoolEntry
    {
        public IBrowserSession Session { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
    }
}
