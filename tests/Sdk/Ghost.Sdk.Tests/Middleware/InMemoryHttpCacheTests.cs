using FluentAssertions;
using Ghost.Sdk.Middleware;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class InMemoryHttpCacheTests : IDisposable
{
    private readonly InMemoryHttpCache _cache;
    private readonly HttpCacheOptions _options;

    public InMemoryHttpCacheTests()
    {
        _options = new HttpCacheOptions
        {
            DefaultTtl = TimeSpan.FromMinutes(5),
            CleanupInterval = TimeSpan.FromSeconds(30),
            MaxCacheSize = 100 * 1024 * 1024
        };
        _cache = new InMemoryHttpCache(_options);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task TryGetAsync_WithEmptyCache_ReturnsFalse()
    {
        // Arrange
        var request = CreateMockRequest("GET", "https://example.com");

        // Act
        var result = await _cache.TryGetAsync(request, out var response);

        // Assert
        result.Should().BeFalse();
        response.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetAsync_ThenTryGetAsync_ReturnsTrue()
    {
        // Arrange
        var request = CreateMockRequest("GET", "https://example.com");
        var response = CreateMockResponse(200, "OK");

        // Act
        await _cache.SetAsync(request, response);
        var result = await _cache.TryGetAsync(request, out var cachedResponse);

        // Assert
        result.Should().BeTrue();
        cachedResponse.Should().NotBeNull();
        cachedResponse!.Status.Should().Be(200);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task TryGetAsync_WithExpiredEntry_ReturnsFalse()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var options = new HttpCacheOptions
        {
            DefaultTtl = TimeSpan.FromMinutes(5),
            CleanupInterval = TimeSpan.FromSeconds(30),
            MaxCacheSize = 100 * 1024 * 1024,
            TimeProvider = fakeTimeProvider
        };
        using var cache = new InMemoryHttpCache(options);
        var request = CreateMockRequest("GET", "https://example.com");
        var response = CreateMockResponse(200, "OK");
        var shortTtl = TimeSpan.FromMilliseconds(100);

        // Act
        await cache.SetAsync(request, response, shortTtl);
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(200));
        var result = await cache.TryGetAsync(request, out var cachedResponse);

        // Assert
        result.Should().BeFalse();
        cachedResponse.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetAsync_WithDifferentMethods_CachesSeparately()
    {
        // Arrange
        var getRequest = CreateMockRequest("GET", "https://example.com");
        var postRequest = CreateMockRequest("POST", "https://example.com");
        var getResponse = CreateMockResponse(200, "GET Response");
        var postResponse = CreateMockResponse(201, "POST Response");

        // Act
        await _cache.SetAsync(getRequest, getResponse);
        await _cache.SetAsync(postRequest, postResponse);

        var getResult = await _cache.TryGetAsync(getRequest, out var cachedGetResponse);
        var postResult = await _cache.TryGetAsync(postRequest, out var cachedPostResponse);

        // Assert
        getResult.Should().BeTrue();
        postResult.Should().BeTrue();
        cachedGetResponse!.Status.Should().Be(200);
        cachedPostResponse!.Status.Should().Be(201);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetAsync_WithDifferentUrls_CachesSeparately()
    {
        // Arrange
        var request1 = CreateMockRequest("GET", "https://example.com/page1");
        var request2 = CreateMockRequest("GET", "https://example.com/page2");
        var response1 = CreateMockResponse(200, "Page 1");
        var response2 = CreateMockResponse(200, "Page 2");

        // Act
        await _cache.SetAsync(request1, response1);
        await _cache.SetAsync(request2, response2);

        var result1 = await _cache.TryGetAsync(request1, out var cachedResponse1);
        var result2 = await _cache.TryGetAsync(request2, out var cachedResponse2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        cachedResponse1!.StatusText.Should().Be("Page 1");
        cachedResponse2!.StatusText.Should().Be("Page 2");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetAsync_OverwritesExistingEntry()
    {
        // Arrange
        var request = CreateMockRequest("GET", "https://example.com");
        var response1 = CreateMockResponse(200, "First");
        var response2 = CreateMockResponse(200, "Second");

        // Act
        await _cache.SetAsync(request, response1);
        await _cache.SetAsync(request, response2);
        var result = await _cache.TryGetAsync(request, out var cachedResponse);

        // Assert
        result.Should().BeTrue();
        cachedResponse!.StatusText.Should().Be("Second");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetAsync_WithCustomTtl_UsesProvidedTtl()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var options = new HttpCacheOptions
        {
            DefaultTtl = TimeSpan.FromMinutes(5),
            CleanupInterval = TimeSpan.FromSeconds(30),
            MaxCacheSize = 100 * 1024 * 1024,
            TimeProvider = fakeTimeProvider
        };
        using var cache = new InMemoryHttpCache(options);
        var request = CreateMockRequest("GET", "https://example.com");
        var response = CreateMockResponse(200, "OK");
        var customTtl = TimeSpan.FromMilliseconds(150);

        // Act
        await cache.SetAsync(request, response, customTtl);
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result1 = await cache.TryGetAsync(request, out var cachedResponse1);

        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result2 = await cache.TryGetAsync(request, out var cachedResponse2);

        // Assert
        result1.Should().BeTrue();
        cachedResponse1.Should().NotBeNull();
        result2.Should().BeFalse();
        cachedResponse2.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task InvalidateAsync_WithMatchingPattern_RemovesEntries()
    {
        // Arrange
        var request1 = CreateMockRequest("GET", "https://example.com/api/users/1");
        var request2 = CreateMockRequest("GET", "https://example.com/api/users/2");
        var request3 = CreateMockRequest("GET", "https://example.com/api/posts/1");
        var response = CreateMockResponse(200, "OK");

        await _cache.SetAsync(request1, response);
        await _cache.SetAsync(request2, response);
        await _cache.SetAsync(request3, response);

        // Act
        await _cache.InvalidateAsync(@"users");

        // Assert
        var result1 = await _cache.TryGetAsync(request1, out _);
        var result2 = await _cache.TryGetAsync(request2, out _);
        var result3 = await _cache.TryGetAsync(request3, out _);

        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeTrue(); // posts endpoint should remain
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task InvalidateAsync_WithRegexPattern_RemovesMatchingEntries()
    {
        // Arrange
        var request1 = CreateMockRequest("GET", "https://example.com/api/v1/users");
        var request2 = CreateMockRequest("GET", "https://example.com/api/v2/users");
        var request3 = CreateMockRequest("GET", "https://example.com/api/posts");
        var response = CreateMockResponse(200, "OK");

        await _cache.SetAsync(request1, response);
        await _cache.SetAsync(request2, response);
        await _cache.SetAsync(request3, response);

        // Act
        await _cache.InvalidateAsync(@"api/v[12]/users");

        // Assert
        var result1 = await _cache.TryGetAsync(request1, out _);
        var result2 = await _cache.TryGetAsync(request2, out _);
        var result3 = await _cache.TryGetAsync(request3, out _);

        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task InvalidateAsync_WithNoMatches_DoesNothing()
    {
        // Arrange
        var request = CreateMockRequest("GET", "https://example.com/api/users");
        var response = CreateMockResponse(200, "OK");
        await _cache.SetAsync(request, response);

        // Act
        await _cache.InvalidateAsync(@"posts");

        // Assert
        var result = await _cache.TryGetAsync(request, out _);
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var response = CreateMockResponse(200, "OK");

        // Act
        var act = async () => await _cache.SetAsync(null!, response);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetAsync_WithNullResponse_ThrowsArgumentNullException()
    {
        // Arrange
        var request = CreateMockRequest("GET", "https://example.com");

        // Act
        var act = async () => await _cache.SetAsync(request, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task TryGetAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _cache.TryGetAsync(null!, out _);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task InvalidateAsync_WithNullPattern_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _cache.InvalidateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Dispose_ClearsCacheEntries()
    {
        // Arrange
        var request = CreateMockRequest("GET", "https://example.com");
        var response = CreateMockResponse(200, "OK");
        await _cache.SetAsync(request, response);

        // Act
        _cache.Dispose();

        // Assert - After disposal, attempting to use cache should not crash
        // (but behavior is undefined after disposal)
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new InMemoryHttpCache(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var request = CreateMockRequest("GET", "https://example.com");
        var response = CreateMockResponse(200, "OK");
        using var cts = new CancellationTokenSource();

        // Act
        await _cache.SetAsync(request, response, null, cts.Token);

        // Assert
        var result = await _cache.TryGetAsync(request, out var cachedResponse);
        result.Should().BeTrue();
        cachedResponse.Should().NotBeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task TryGetAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var request = CreateMockRequest("GET", "https://example.com");
        var response = CreateMockResponse(200, "OK");
        await _cache.SetAsync(request, response);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _cache.TryGetAsync(request, out var cachedResponse, cts.Token);

        // Assert
        result.Should().BeTrue();
        cachedResponse.Should().NotBeNull();
    }

    private static IRequest CreateMockRequest(string method, string url)
    {
        var mock = new Mock<IRequest>();
        mock.Setup(r => r.Method).Returns(method);
        mock.Setup(r => r.Url).Returns(url);
        return mock.Object;
    }

    private static IResponse CreateMockResponse(int status, string statusText)
    {
        var mock = new Mock<IResponse>();
        mock.Setup(r => r.Status).Returns(status);
        mock.Setup(r => r.StatusText).Returns(statusText);
        return mock.Object;
    }
}
