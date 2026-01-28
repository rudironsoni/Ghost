using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghost.Services;

public class RotatingProxyProvider : IProxyProvider
    , IDisposable
{
    private readonly IEnumerable<IProxySource> _sources;
    private readonly ILogger<RotatingProxyProvider> _logger;

    // Snapshot of proxies; volatile to ensure reads see fully initialized array
    private volatile ProxyInfo[]? _proxies;

    // Simple counter for round-robin selection
    private long _index;

    // Ensure only one initializer runs at a time
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly Action<ILogger, Exception?> s_logInitCancelled =
        LoggerMessage.Define(LogLevel.Debug, new EventId(1, nameof(RotatingProxyProvider)), "Proxy initialization cancelled");

    private static readonly Action<ILogger, string, Exception?> s_logFetchFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, nameof(RotatingProxyProvider)), "Failed to fetch proxies from source {Source}");

    private static readonly Action<ILogger, int, Exception?> s_logInitialized =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(RotatingProxyProvider)), "Initialized {Count} proxies");

    public RotatingProxyProvider(IEnumerable<IProxySource> sources, ILogger<RotatingProxyProvider> logger)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken token = default)
    {
        // Lazy initialize proxies on first call
        var snapshot = _proxies;
        if (snapshot == null || snapshot.Length == 0)
        {
            await EnsureInitializedAsync(token).ConfigureAwait(false);
            snapshot = _proxies;
        }

        // If still empty, return null
        if (snapshot == null || snapshot.Length == 0)
            return null;

        // Round-robin selection using atomic increment
        var idx = (int)(Interlocked.Increment(ref _index) % snapshot.Length);
        if (idx < 0) idx = 0;
        return snapshot[idx];
    }

    private async Task EnsureInitializedAsync(CancellationToken token)
    {
        // Fast-path check
        var snapshot = _proxies;
        if (snapshot != null && snapshot.Length > 0)
            return;

        await _initLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // Re-check under lock
            snapshot = _proxies;
            if (snapshot != null && snapshot.Length > 0)
                return;

            var list = new List<ProxyInfo>();
            foreach (var src in _sources)
            {
                try
                {
                    var proxies = await src.FetchProxiesAsync(token).ConfigureAwait(false);
                    if (proxies != null)
                        list.AddRange(proxies);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    s_logInitCancelled(_logger, null);
                    throw;
                }
                catch (Exception ex)
                {
                    s_logFetchFailed(_logger, src?.GetType().FullName ?? string.Empty, ex);
                }
            }

            // Remove duplicates and store snapshot
            _proxies = list.Distinct().ToArray();
            s_logInitialized(_logger, _proxies.Length, null);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void Dispose()
    {
        _initLock?.Dispose();
        GC.SuppressFinalize(this);
    }
}
