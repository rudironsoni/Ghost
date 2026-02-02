using System.Collections.Generic;
using System.Linq;

namespace Ghost.Proxy;

/// <summary>
/// Aggregated health report for a collection of proxy endpoints.
/// </summary>
public class ProxyHealthReport
{
    /// <summary>
    /// Gets or sets the status entries for each proxy.
    /// </summary>
    public List<ProxyStatus> Proxies { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of proxies considered healthy.
    /// </summary>
    public int HealthyCount { get; set; }

    /// <summary>
    /// Gets or sets the number of proxies considered unhealthy.
    /// </summary>
    public int UnhealthyCount { get; set; }

    /// <summary>
    /// Gets the healthy proxies ordered by latency.
    /// </summary>
    public List<string> GetHealthyProxiesSortedByLatency()
    {
        return Proxies
            .Where(proxy => proxy.IsHealthy)
            .OrderBy(proxy => proxy.LatencyMs)
            .Select(proxy => proxy.Url)
            .ToList();
    }
}
