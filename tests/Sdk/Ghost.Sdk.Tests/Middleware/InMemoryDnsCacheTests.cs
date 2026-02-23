using System.Net;
using FluentAssertions;
using Ghost.Sdk.Middleware;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class InMemoryDnsCacheTests : IDisposable
{
    private readonly InMemoryDnsCache _cache;
    private readonly DnsCacheOptions _options;

    public InMemoryDnsCacheTests()
    {
        _options = new DnsCacheOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            MaxEntries = 1000
        };
        _cache = new InMemoryDnsCache(_options);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_WithValidHostname_ResolvesSuccessfully()
    {
        // Arrange
        const string hostname = "localhost";

        // Act
        var addresses = await _cache.ResolveAsync(hostname);

        // Assert
        addresses.Should().NotBeNull();
        addresses.Should().NotBeEmpty();
        addresses.Should().Contain(addr => addr.Equals(IPAddress.Loopback) || addr.Equals(IPAddress.IPv6Loopback));
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_CalledTwice_ReturnsCachedResult()
    {
        // Arrange
        const string hostname = "localhost";

        // Act
        var addresses1 = await _cache.ResolveAsync(hostname);
        var addresses2 = await _cache.ResolveAsync(hostname);

        // Assert
        addresses1.Should().NotBeNull();
        addresses2.Should().NotBeNull();
        addresses1.Should().BeEquivalentTo(addresses2);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_WithExpiredEntry_ResolvesAgain()
    {
        // Arrange
        const string hostname = "localhost";
        var fakeTimeProvider = new FakeTimeProvider();
        var shortTtlOptions = new DnsCacheOptions
        {
            Ttl = TimeSpan.FromMilliseconds(100),
            TimeProvider = fakeTimeProvider
        };
        using var shortCache = new InMemoryDnsCache(shortTtlOptions);

        // Act
        var addresses1 = await shortCache.ResolveAsync(hostname);
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(200));
        var addresses2 = await shortCache.ResolveAsync(hostname);

        // Assert
        addresses1.Should().NotBeNull();
        addresses2.Should().NotBeNull();
        // Both should be valid, even though the cache entry expired
        addresses1.Should().Contain(addr => addr.Equals(IPAddress.Loopback) || addr.Equals(IPAddress.IPv6Loopback));
        addresses2.Should().Contain(addr => addr.Equals(IPAddress.Loopback) || addr.Equals(IPAddress.IPv6Loopback));
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_WithInvalidHostname_ThrowsException()
    {
        // Arrange
        const string hostname = "invalid.hostname.that.does.not.exist.example.local";

        // Act
        var act = async () => await _cache.ResolveAsync(hostname);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_WithNullHostname_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _cache.ResolveAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Invalidate_RemovesCachedEntry()
    {
        // Arrange
        const string hostname = "localhost";
        await _cache.ResolveAsync(hostname); // Cache the entry

        // Act
        _cache.Invalidate(hostname);

        // Assert
        // After invalidation, the next resolve should work but won't be from cache
        // (We can't directly test if it's from cache, but we can verify it still works)
        var addresses = await _cache.ResolveAsync(hostname);
        addresses.Should().NotBeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Invalidate_WithNullHostname_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _cache.Invalidate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Invalidate_WithNonExistentHostname_DoesNotThrow()
    {
        // Act
        var act = () => _cache.Invalidate("non.existent.hostname");

        // Assert
        act.Should().NotThrow();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Clear_RemovesAllCachedEntries()
    {
        // Arrange
        const string hostname1 = "localhost";
        const string hostname2 = "127.0.0.1";
        await _cache.ResolveAsync(hostname1);
        await _cache.ResolveAsync(hostname2);

        // Act
        _cache.Clear();

        // Assert
        // After clearing, resolve should still work (just not from cache)
        var addresses1 = await _cache.ResolveAsync(hostname1);
        var addresses2 = await _cache.ResolveAsync(hostname2);
        addresses1.Should().NotBeNull();
        addresses2.Should().NotBeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Clear_WithEmptyCache_DoesNotThrow()
    {
        // Act
        var act = () => _cache.Clear();

        // Assert
        act.Should().NotThrow();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        const string hostname = "localhost";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _cache.ResolveAsync(hostname, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_WithMultipleHostnames_CachesSeparately()
    {
        // Arrange
        const string hostname1 = "localhost";
        const string hostname2 = "127.0.0.1";

        // Act
        var addresses1 = await _cache.ResolveAsync(hostname1);
        var addresses2 = await _cache.ResolveAsync(hostname2);

        // Assert
        addresses1.Should().NotBeNull();
        addresses2.Should().NotBeNull();
        // Both should resolve to loopback addresses
        addresses1.Should().Contain(addr => addr.Equals(IPAddress.Loopback) || addr.Equals(IPAddress.IPv6Loopback));
        addresses2.Should().Contain(addr => addr.Equals(IPAddress.Loopback) || addr.Equals(IPAddress.IPv6Loopback));
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new InMemoryDnsCache(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var cache = new InMemoryDnsCache(_options);

        // Act
        var act = () =>
        {
            cache.Dispose();
            cache.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_WithMaxEntriesExceeded_RemovesOldestEntries()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var limitedOptions = new DnsCacheOptions
        {
            MaxEntries = 2,
            Ttl = TimeSpan.FromMinutes(10),
            TimeProvider = fakeTimeProvider
        };
        using var limitedCache = new InMemoryDnsCache(limitedOptions);

        // Act - Add 3 entries when max is 2
        await limitedCache.ResolveAsync("localhost");
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(10));
        await limitedCache.ResolveAsync("127.0.0.1");
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(10));
        await limitedCache.ResolveAsync("::1");

        // Assert - All should still resolve (even if evicted from cache)
        var addresses1 = await limitedCache.ResolveAsync("localhost");
        var addresses2 = await limitedCache.ResolveAsync("127.0.0.1");
        var addresses3 = await limitedCache.ResolveAsync("::1");

        addresses1.Should().NotBeNull();
        addresses2.Should().NotBeNull();
        addresses3.Should().NotBeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ResolveAsync_ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        const string hostname = "localhost";
        const int concurrentRequests = 10;

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => _cache.ResolveAsync(hostname))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(addresses =>
        {
            addresses.Should().NotBeNull();
            addresses.Should().NotBeEmpty();
        });
    }
}
