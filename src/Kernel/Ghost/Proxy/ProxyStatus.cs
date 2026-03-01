using System;

namespace Ghost.Proxy;

/// <summary>
/// Represents the health status of a single proxy endpoint.
/// </summary>
public class ProxyStatus
{
    /// <summary>
    /// Gets or sets the proxy URL that was checked.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the proxy is considered healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the measured latency in milliseconds.
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the error message when the proxy check fails.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp for the last health check.
    /// </summary>
    public DateTime LastChecked { get; set; }
}
