using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ghost.Tests.ProxyManagement;

public sealed class RotatingProxyPoolTests
{
    [Fact]
    public async Task GetNextProxyAsync_WithEmptyPool_ReturnsNull()
    {
        // Arrange
        var mockScraper = new Mock<IProxySource>();
        mockScraper.Setup(s => s.FetchProxiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProxyInfo>() as System.Collections.Generic.IEnumerable<ProxyInfo>);

        var healthChecker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);
        var pool = new RotatingProxyPool(mockScraper.Object, healthChecker, NullLogger<RotatingProxyPool>.Instance);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ProxyInfo? proxy = await pool.GetNextProxyAsync(cts.Token);

        // Assert
        proxy.Should().BeNull();
    }

    [Fact]
    public async Task HealthyProxyCount_InitiallyZero()
    {
        // Arrange
        var mockScraper = new Mock<IProxySource>();
        mockScraper.Setup(s => s.FetchProxiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProxyInfo>() as System.Collections.Generic.IEnumerable<ProxyInfo>);

        var healthChecker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);
        var pool = new RotatingProxyPool(mockScraper.Object, healthChecker, NullLogger<RotatingProxyPool>.Instance);

        // Act & Assert
        pool.HealthyProxyCount.Should().Be(0);
    }

    [Fact]
    public async Task ReportProxyResultAsync_WithNullProxy_ThrowsArgumentNullException()
    {
        // Arrange
        var mockScraper = new Mock<IProxySource>();
        var healthChecker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);
        var pool = new RotatingProxyPool(mockScraper.Object, healthChecker, NullLogger<RotatingProxyPool>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await pool.ReportProxyResultAsync(null!, true, TimeSpan.Zero).ConfigureAwait(false));
    }

    [Fact]
    public void GetAllProxies_InitiallyEmpty()
    {
        // Arrange
        var mockScraper = new Mock<IProxySource>();
        var healthChecker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);
        var pool = new RotatingProxyPool(mockScraper.Object, healthChecker, NullLogger<RotatingProxyPool>.Instance);

        // Act
        IReadOnlyList<ProxyPoolEntry> proxies = pool.GetAllProxies();

        // Assert
        proxies.Should().BeEmpty();
    }
}
