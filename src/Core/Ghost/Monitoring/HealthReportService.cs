using Ghost.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghost.Monitoring;

/// <summary>
/// Default implementation that builds a basic health report.
/// </summary>
public sealed class HealthReportService : IHealthReportService
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly Action<ILogger, string, Exception?> ProxyProbeTimedOutLog =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(6001, nameof(ProxyProbeTimedOutLog)),
            "Proxy health probe timed out for source {ProxySource}");

    private static readonly Action<ILogger, string, Exception?> ProxyProbeFailedLog =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(6002, nameof(ProxyProbeFailedLog)),
            "Proxy health probe failed for source {ProxySource}");

    private readonly IEnumerable<IProxySource> _proxySources;
    private readonly IEnumerable<IJobScraper> _jobScrapers;
    private readonly ILogger<HealthReportService> _logger;

    /// <summary>
    /// Creates a new health report service.
    /// </summary>
    public HealthReportService(
        IEnumerable<IProxySource> proxySources,
        IEnumerable<IJobScraper> jobScrapers,
        ILogger<HealthReportService> logger)
    {
        _proxySources = proxySources ?? Array.Empty<IProxySource>();
        _jobScrapers = jobScrapers ?? Array.Empty<IJobScraper>();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthReport> BuildReportAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var platformHealth = _jobScrapers
            .Where(scraper => !string.IsNullOrWhiteSpace(scraper.PlatformName))
            .GroupBy(scraper => scraper.PlatformName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PlatformHealth
            {
                Name = group.Key,
                IsHealthy = group.Any()
            })
            .OrderBy(platform => platform.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var proxyHealth = new List<ProxyHealth>();
        foreach (var proxySource in _proxySources)
        {
            proxyHealth.Add(await BuildProxyHealthAsync(proxySource, ct));
        }

        var report = new HealthReport
        {
            Platforms = platformHealth,
            Proxies = proxyHealth
        };

        return report;
    }

    private async Task<ProxyHealth> BuildProxyHealthAsync(IProxySource proxySource, CancellationToken ct)
    {
        var sourceName = proxySource.GetType().Name;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ProbeTimeout);

            var proxies = await proxySource.FetchProxiesAsync(timeoutCts.Token).ConfigureAwait(false);
            var firstProxy = proxies.FirstOrDefault();

            if (firstProxy is null)
            {
                return new ProxyHealth
                {
                    Url = sourceName,
                    IsHealthy = false
                };
            }

            return new ProxyHealth
            {
                Url = firstProxy.Server,
                IsHealthy = true
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            ProxyProbeTimedOutLog(_logger, sourceName, null);
            return new ProxyHealth
            {
                Url = sourceName,
                IsHealthy = false
            };
        }
        catch (Exception ex)
        {
            ProxyProbeFailedLog(_logger, sourceName, ex);
            return new ProxyHealth
            {
                Url = sourceName,
                IsHealthy = false
            };
        }
    }
}
