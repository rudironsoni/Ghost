using System.Collections.Generic;

namespace Ghost.ProxyConfiguration;

/// <summary>
/// Configuration options for the abstract proxy system.
/// Provides centralized configuration for managing multiple proxy sources
/// with flexible rotation strategies and health monitoring.
/// </summary>
public class ProxySystemOptions
{
    /// <summary>
    /// Collection of configured proxy sources.
    /// Supports multiple providers simultaneously (static hosts, APIs, etc.)
    /// </summary>
    public List<ProxySourceConfig> Sources { get; set; } = new();

    /// <summary>
    /// Proxy rotation strategy to use when multiple proxies are available.
    /// Common values: "RoundRobin", "Performance", "Random", "LeastUsed"
    /// Defaults to "RoundRobin" for deterministic behavior.
    /// </summary>
    public string RotationStrategy { get; set; } = "RoundRobin";

    /// <summary>
    /// Interval in seconds for periodic proxy health checks.
    /// Set to 0 to disable automatic health checking.
    /// Defaults to 300 seconds (5 minutes).
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Fallback proxy sources to use when primary sources fail.
    /// Enables graceful degradation even if all primary sources become unavailable.
    /// Can include free third-party API proxies for resilience.
    /// </summary>
    public List<ProxySourceConfig> FallbackChain { get; set; } = new();
}
