namespace Ghost.Monitoring;

/// <summary>
/// Health report with platform and proxy details.
/// </summary>
public sealed class HealthReport
{
    /// <summary>
    /// Platform health statuses.
    /// </summary>
    public IReadOnlyList<PlatformHealth> Platforms { get; init; } = Array.Empty<PlatformHealth>();

    /// <summary>
    /// Proxy health statuses.
    /// </summary>
    public IReadOnlyList<ProxyHealth> Proxies { get; init; } = Array.Empty<ProxyHealth>();
}

/// <summary>
/// Platform health status.
/// </summary>
public sealed class PlatformHealth
{
    /// <summary>
    /// Platform name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether the platform is healthy.
    /// </summary>
    public bool IsHealthy { get; init; }
}

/// <summary>
/// Proxy health status.
/// </summary>
public sealed class ProxyHealth
{
    /// <summary>
    /// Proxy URL.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Whether the proxy is healthy.
    /// </summary>
    public bool IsHealthy { get; init; }
}
