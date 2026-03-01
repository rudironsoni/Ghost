using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.ProxyManagement;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.ProxyManagement;

public sealed class ProxyGeographicFilterTests : ReliabilityTestBase
{
    public ProxyGeographicFilterTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task GetGeolocationAsync_WithNullIp_ThrowsArgumentException()
    {
        // Arrange
        var filter = new ProxyGeographicFilter(NullLogger<ProxyGeographicFilter>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await filter.GetGeolocationAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetGeolocationAsync_WithEmptyIp_ThrowsArgumentException()
    {
        // Arrange
        var filter = new ProxyGeographicFilter(NullLogger<ProxyGeographicFilter>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await filter.GetGeolocationAsync(string.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task EnrichProxiesAsync_WithNullProxies_ThrowsArgumentNullException()
    {
        // Arrange
        var filter = new ProxyGeographicFilter(NullLogger<ProxyGeographicFilter>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await filter.EnrichProxiesAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task EnrichProxiesAsync_WithEmptyProxies_ReturnsEmptyDictionary()
    {
        // Arrange
        var filter = new ProxyGeographicFilter(NullLogger<ProxyGeographicFilter>.Instance);
        ProxyInfo[] proxies = Array.Empty<Ghost.ProxyInfo>();

        // Act
        Dictionary<string, ProxyGeolocation> result = await filter.EnrichProxiesAsync(proxies, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
