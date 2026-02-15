using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Tests.ProxyManagement;

public sealed class ProxyGeographicFilterTests
{
    [Fact]
    public async Task GetGeolocationAsync_WithNullIp_ThrowsArgumentException()
    {
        // Arrange
        var filter = new ProxyGeographicFilter(NullLogger<ProxyGeographicFilter>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await filter.GetGeolocationAsync(null!, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [Fact]
    public async Task GetGeolocationAsync_WithEmptyIp_ThrowsArgumentException()
    {
        // Arrange
        var filter = new ProxyGeographicFilter(NullLogger<ProxyGeographicFilter>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await filter.GetGeolocationAsync(string.Empty, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [Fact]
    public async Task EnrichProxiesAsync_WithNullProxies_ThrowsArgumentNullException()
    {
        // Arrange
        var filter = new ProxyGeographicFilter(NullLogger<ProxyGeographicFilter>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await filter.EnrichProxiesAsync(null!, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [Fact]
    public async Task EnrichProxiesAsync_WithEmptyProxies_ReturnsEmptyDictionary()
    {
        // Arrange
        var filter = new ProxyGeographicFilter(NullLogger<ProxyGeographicFilter>.Instance);
        ProxyInfo[] proxies = Array.Empty<Ghost.Abstractions.ProxyInfo>();

        // Act
        Dictionary<string, ProxyGeolocation> result = await filter.EnrichProxiesAsync(proxies, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeEmpty();
    }
}
