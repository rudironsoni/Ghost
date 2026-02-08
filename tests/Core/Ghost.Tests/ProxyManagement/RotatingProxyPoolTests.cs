using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ghost.Tests.ProxyManagement;

public sealed class RotatingProxyPoolTests
{
    [Fact]
    public async Task GetNextProxyAsync_WithEmptyPool_ReturnsNull()
    {
        // Arrange
        var scraper = Substitute.For<IProxySource>();
        scraper.FetchProxiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<ProxyInfo>() as System.Collections.Generic.IEnumerable<ProxyInfo>));

        var healthChecker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);
        var pool = new RotatingProxyPool(scraper, healthChecker, NullLogger<RotatingProxyPool>.Instance);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var proxy = await pool.GetNextProxyAsync(cts.Token);

        // Assert
        proxy.Should().BeNull();
    }

    [Fact]
    public async Task HealthyProxyCount_InitiallyZero()
    {
        // Arrange
        var scraper = Substitute.For<IProxySource>();
        scraper.FetchProxiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<ProxyInfo>() as System.Collections.Generic.IEnumerable<ProxyInfo>));

        var healthChecker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);
        var pool = new RotatingProxyPool(scraper, healthChecker, NullLogger<RotatingProxyPool>.Instance);

        // Act & Assert
        pool.HealthyProxyCount.Should().Be(0);
    }

    [Fact]
    public async Task ReportProxyResultAsync_WithNullProxy_ThrowsArgumentNullException()
    {
        // Arrange
        var scraper = Substitute.For<IProxySource>();
        var healthChecker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);
        var pool = new RotatingProxyPool(scraper, healthChecker, NullLogger<RotatingProxyPool>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await pool.ReportProxyResultAsync(null!, true, TimeSpan.Zero));
    }

    [Fact]
    public void GetAllProxies_InitiallyEmpty()
    {
        // Arrange
        var scraper = Substitute.For<IProxySource>();
        var healthChecker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);
        var pool = new RotatingProxyPool(scraper, healthChecker, NullLogger<RotatingProxyPool>.Instance);

        // Act
        var proxies = pool.GetAllProxies();

        // Assert
        proxies.Should().BeEmpty();
    }
}
