using System.Net;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Interface for managing proxy servers with rotation and health checking.
/// </summary>
/// <remarks>
/// Implementations of this interface provide proxy rotation logic with failure tracking
/// to route requests through proxy servers while avoiding failed or unhealthy proxies.
/// </remarks>
public interface IProxyManager
{
    /// <summary>
    /// Gets the next available proxy from the pool.
    /// </summary>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>A WebProxy instance, or null if no proxies are available.</returns>
    /// <remarks>
    /// This method uses the configured rotation strategy (e.g., round-robin) and
    /// automatically skips proxies that have exceeded the failure threshold.
    /// </remarks>
    Task<WebProxy?> GetNextProxyAsync(CancellationToken ct = default);

    /// <summary>
    /// Reports a successful request through the specified proxy.
    /// </summary>
    /// <param name="proxy">The proxy that successfully handled the request.</param>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Success reports may reset failure counters or improve the proxy's health score.
    /// </remarks>
    Task ReportSuccessAsync(WebProxy proxy, CancellationToken ct = default);

    /// <summary>
    /// Reports a failed request through the specified proxy.
    /// </summary>
    /// <param name="proxy">The proxy that failed to handle the request.</param>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Failure reports increment the proxy's failure counter. Once the failure count
    /// exceeds the configured threshold, the proxy will be temporarily excluded from rotation.
    /// </remarks>
    Task ReportFailureAsync(WebProxy proxy, CancellationToken ct = default);

    /// <summary>
    /// Adds a proxy to the pool.
    /// </summary>
    /// <param name="host">The proxy host address.</param>
    /// <param name="port">The proxy port number.</param>
    /// <param name="username">Optional username for proxy authentication.</param>
    /// <param name="password">Optional password for proxy authentication.</param>
    /// <remarks>
    /// If username is provided, the proxy will be configured with NetworkCredential
    /// for basic authentication. Password is required when username is specified.
    /// </remarks>
    void AddProxy(string host, int port, string? username = null, string? password = null);
}
