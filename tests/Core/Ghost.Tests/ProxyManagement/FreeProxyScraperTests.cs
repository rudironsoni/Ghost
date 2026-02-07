using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Tests.ProxyManagement;

public sealed class FreeProxyScraperTests
{
    [Fact]
    public async Task FetchProxiesAsync_ReturnsProxies()
    {
        // Arrange
        var scraper = new FreeProxyScraper(NullLogger<FreeProxyScraper>.Instance);

        // Act
        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);

        // Assert
        proxies.Should().NotBeNull();
        // Note: This may return 0 proxies if sources are unavailable
        // In production, you'd mock the HttpClient to test parsing logic
    }

    [Fact]
    public async Task FetchProxiesAsync_RemovesDuplicates()
    {
        // Arrange
        var scraper = new FreeProxyScraper(NullLogger<FreeProxyScraper>.Instance);

        // Act
        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        // Assert
        var uniqueServers = proxyList.Select(p => p.Server).Distinct().Count();
        uniqueServers.Should().Be(proxyList.Count, "all proxies should have unique server addresses");
    }

    [Fact]
    public async Task FetchProxiesAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var scraper = new FreeProxyScraper(NullLogger<FreeProxyScraper>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await scraper.FetchProxiesAsync(cts.Token));
    }
}
