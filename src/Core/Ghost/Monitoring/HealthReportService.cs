using Ghost.Abstractions;

namespace Ghost.Monitoring;

/// <summary>
/// Default implementation that builds a basic health report.
/// </summary>
public sealed class HealthReportService : IHealthReportService
{
    private readonly IEnumerable<IProxySource> _proxySources;

    /// <summary>
    /// Creates a new health report service.
    /// </summary>
    public HealthReportService(IEnumerable<IProxySource> proxySources)
    {
        _proxySources = proxySources ?? Array.Empty<IProxySource>();
    }

    /// <inheritdoc />
    public Task<HealthReport> BuildReportAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var report = new HealthReport
        {
            Platforms = Array.Empty<PlatformHealth>(),
            Proxies = _proxySources
                .Select(_ => new ProxyHealth { Url = string.Empty, IsHealthy = true })
                .ToArray()
        };

        return Task.FromResult(report);
    }
}
