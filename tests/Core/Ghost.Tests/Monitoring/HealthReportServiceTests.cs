using FluentAssertions;
using Ghost.Abstractions;
using Ghost.Monitoring;
using Xunit;

namespace Ghost.Tests.Monitoring;

public class HealthReportServiceTests
{
    [Fact]
    public async Task BuildReportAsync_ReturnsEmptyLists_WhenNoProxySources()
    {
        var service = new HealthReportService(Array.Empty<IProxySource>());

        var report = await service.BuildReportAsync(CancellationToken.None);

        report.Platforms.Should().BeEmpty();
        report.Proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildReportAsync_ReturnsOneProxyEntryPerSource()
    {
        var sources = new[]
        {
            new TestProxySource(),
            new TestProxySource(),
            new TestProxySource()
        };
        var service = new HealthReportService(sources);

        var report = await service.BuildReportAsync(CancellationToken.None);

        report.Proxies.Should().HaveCount(3);
    }

    [Fact]
    public async Task BuildReportAsync_PopulatesHealthyProxyEntries()
    {
        var service = new HealthReportService(new[] { new TestProxySource() });

        var report = await service.BuildReportAsync(CancellationToken.None);

        report.Proxies.Should().AllSatisfy(proxy => proxy.IsHealthy.Should().BeTrue());
    }

    [Fact]
    public async Task BuildReportAsync_ThrowsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = new HealthReportService(Array.Empty<IProxySource>());

        var act = () => service.BuildReportAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class TestProxySource : IProxySource
    {
        public Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
        {
            return Task.FromResult<IEnumerable<ProxyInfo>>(Array.Empty<ProxyInfo>());
        }
    }
}
