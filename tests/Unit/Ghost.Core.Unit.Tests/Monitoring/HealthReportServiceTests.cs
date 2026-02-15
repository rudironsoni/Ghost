using FluentAssertions;
using Ghost.Abstractions;
using Ghost.Monitoring;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ghost.Core.Unit.Tests.Monitoring;

public sealed class HealthReportServiceTests
{
    [Fact]
    public async Task BuildReportAsync_ShouldIncludePlatformAndProxyHealth()
    {
        IJobScraper google = Substitute.For<IJobScraper>();
        google.PlatformName.Returns("Google");

        IProxySource healthyProxy = Substitute.For<IProxySource>();
        healthyProxy
            .FetchProxiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<ProxyInfo>>(new[]
            {
                new ProxyInfo("proxy://healthy", null, null)
            }));

        IProxySource emptyProxy = Substitute.For<IProxySource>();
        emptyProxy
            .FetchProxiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<ProxyInfo>>(Array.Empty<ProxyInfo>()));

        var sut = new HealthReportService(new[] { healthyProxy, emptyProxy }, new[] { google }, NullLogger<HealthReportService>.Instance);

        HealthReport report = await sut.BuildReportAsync(CancellationToken.None).ConfigureAwait(false);

        report.Platforms.Should().ContainSingle(p => p.Name == "Google" && p.IsHealthy);
        report.Proxies.Should().HaveCount(2);
        report.Proxies.Should().Contain(p => p.Url == "proxy://healthy" && p.IsHealthy);
        report.Proxies.Should().Contain(p => !p.IsHealthy);
    }

    [Fact]
    public async Task BuildReportAsync_ShouldMarkProxyUnhealthy_WhenProbeThrows()
    {
        IProxySource throwingProxy = Substitute.For<IProxySource>();
        throwingProxy
            .FetchProxiesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IEnumerable<ProxyInfo>>>(_ => throw new InvalidOperationException("probe-failed"));

        var sut = new HealthReportService(new[] { throwingProxy }, Array.Empty<IJobScraper>(), NullLogger<HealthReportService>.Instance);

        HealthReport report = await sut.BuildReportAsync(CancellationToken.None).ConfigureAwait(false);

        report.Proxies.Should().ContainSingle();
        report.Proxies[0].IsHealthy.Should().BeFalse();
        report.Proxies[0].Url.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task BuildReportAsync_ShouldHonorCancellation()
    {
        var sut = new HealthReportService(Array.Empty<IProxySource>(), Array.Empty<IJobScraper>(), NullLogger<HealthReportService>.Instance);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        Func<Task<HealthReport>> act = async () => await sut.BuildReportAsync(cts.Token).ConfigureAwait(false);

        await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }
}
