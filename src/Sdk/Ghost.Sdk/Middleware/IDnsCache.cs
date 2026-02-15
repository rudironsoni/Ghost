using System.Net;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Interface for DNS caching to avoid repeated domain resolution.
/// </summary>
public interface IDnsCache
{
    /// <summary>
    /// Resolves a hostname to IP addresses, using cache when available.
    /// </summary>
    /// <param name="hostname">The hostname to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An array of IP addresses for the hostname.</returns>
    public Task<IPAddress[]> ResolveAsync(string hostname, CancellationToken ct = default);

    /// <summary>
    /// Invalidates the cached DNS entry for the specified hostname.
    /// </summary>
    /// <param name="hostname">The hostname to invalidate.</param>
    public void Invalidate(string hostname);

    /// <summary>
    /// Clears all cached DNS entries.
    /// </summary>
    public void Clear();
}
