using System.Collections.Concurrent;
using System.Net;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Manages proxy servers with round-robin rotation and failure tracking.
/// </summary>
/// <remarks>
/// This implementation provides thread-safe proxy rotation using round-robin strategy.
/// Proxies that exceed the configured failure threshold are automatically excluded from
/// rotation until they recover or the retry period elapses.
/// </remarks>
public class ProxyManager : IProxyManager
{
    private readonly ConcurrentDictionary<string, ProxyInfo> _proxies = new();
    private readonly ProxyOptions _options;
    private int _currentIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProxyManager"/> class.
    /// </summary>
    /// <param name="options">Configuration options for proxy behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public ProxyManager(ProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Gets the next available proxy using round-robin rotation.
    /// </summary>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>A WebProxy instance, or null if no healthy proxies are available.</returns>
    /// <remarks>
    /// This method automatically skips proxies that have exceeded the failure threshold
    /// and haven't passed the retry period. If all proxies are unhealthy, returns null.
    /// The rotation is thread-safe using atomic operations.
    /// </remarks>
    public Task<WebProxy?> GetNextProxyAsync(CancellationToken ct = default)
    {
        if (_proxies.IsEmpty)
        {
            return Task.FromResult<WebProxy?>(null);
        }

        // Get available proxies (not exceeding max failures or within retry window)
        var available = _proxies
            .Where(p => IsProxyAvailable(p.Value))
            .Select(p => p.Value.Proxy)
            .ToList();

        if (available.Count == 0)
        {
            return Task.FromResult<WebProxy?>(null);
        }

        // Round-robin selection
        int index = Interlocked.Increment(ref _currentIndex) % available.Count;
        return Task.FromResult<WebProxy?>(available[index]);
    }

    /// <summary>
    /// Reports a successful request through the specified proxy.
    /// </summary>
    /// <param name="proxy">The proxy that successfully handled the request.</param>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Success reports reset the failure counter and update the last failure timestamp,
    /// allowing previously failed proxies to rejoin the rotation pool.
    /// </remarks>
    public Task ReportSuccessAsync(WebProxy proxy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        string key = GetProxyKey(proxy);
        if (_proxies.TryGetValue(key, out ProxyInfo? info))
        {
            // Reset failure count on success
            Interlocked.Exchange(ref info.FailureCount, 0);
            info.LastFailure = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reports a failed request through the specified proxy.
    /// </summary>
    /// <param name="proxy">The proxy that failed to handle the request.</param>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Failure reports increment the proxy's failure counter. Once the failure count
    /// exceeds MaxFailures, the proxy is temporarily excluded from rotation until
    /// the RetryAfter period elapses.
    /// </remarks>
    public Task ReportFailureAsync(WebProxy proxy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        string key = GetProxyKey(proxy);
        if (_proxies.TryGetValue(key, out ProxyInfo? info))
        {
            Interlocked.Increment(ref info.FailureCount);
            info.LastFailure = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a proxy to the pool.
    /// </summary>
    /// <param name="host">The proxy host address.</param>
    /// <param name="port">The proxy port number.</param>
    /// <param name="username">Optional username for proxy authentication.</param>
    /// <param name="password">Optional password for proxy authentication.</param>
    /// <exception cref="ArgumentException">Thrown when host is null or empty.</exception>
    /// <remarks>
    /// If username is provided, the proxy will be configured with NetworkCredential
    /// for basic authentication. The proxy is immediately available for rotation.
    /// </remarks>
    public void AddProxy(string host, int port, string? username = null, string? password = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);

        var proxy = new WebProxy(host, port);
        if (!string.IsNullOrEmpty(username))
        {
            proxy.Credentials = new NetworkCredential(username, password);
        }

        string key = $"{host}:{port}";
        _proxies[key] = new ProxyInfo { Proxy = proxy, FailureCount = 0 };
    }

    /// <summary>
    /// Determines if a proxy is available for rotation.
    /// </summary>
    /// <param name="info">The proxy info to check.</param>
    /// <returns>True if the proxy is healthy or has passed the retry period; otherwise, false.</returns>
    /// <remarks>
    /// A proxy is considered available if:
    /// - Its failure count is below MaxFailures, OR
    /// - Its last failure occurred before the RetryAfter period
    /// </remarks>
    private bool IsProxyAvailable(ProxyInfo info)
    {
        if (info.FailureCount < _options.MaxFailures)
        {
            return true;
        }

        // Check if retry period has elapsed
        if (info.LastFailure.HasValue)
        {
            TimeSpan timeSinceFailure = DateTime.UtcNow - info.LastFailure.Value;
            if (timeSinceFailure >= _options.RetryAfter)
            {
                // Reset failure count after retry period
                Interlocked.Exchange(ref info.FailureCount, 0);
                info.LastFailure = null;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Generates a unique key for the proxy based on its address.
    /// </summary>
    /// <param name="proxy">The proxy to generate a key for.</param>
    /// <returns>A string key in the format "host:port".</returns>
    private static string GetProxyKey(WebProxy proxy)
    {
        Uri? address = proxy.Address;
        return address != null
            ? $"{address.Host}:{address.Port}"
            : string.Empty;
    }

    /// <summary>
    /// Internal class to track proxy health and failure statistics.
    /// </summary>
    private sealed class ProxyInfo
    {
        /// <summary>
        /// Gets or sets the WebProxy instance.
        /// </summary>
        public required WebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets the number of consecutive failures.
        /// </summary>
        public long FailureCount;

        /// <summary>
        /// Gets or sets the timestamp of the last failure.
        /// </summary>
        public DateTime? LastFailure { get; set; }
    }
}
