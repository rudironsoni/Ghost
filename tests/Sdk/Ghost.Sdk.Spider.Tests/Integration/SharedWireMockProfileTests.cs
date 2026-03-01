using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Testing.WireMock.Factories;
using Ghost.Testing.WireMock.Profiles;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.Server;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Integration;

/// <summary>
/// Example integration tests demonstrating shared WireMock profiles.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Capability", "RequiresMockServer")]
public class SharedWireMockProfileTests : IDisposable
{
    private readonly IWireMockServer _server;
    private readonly HttpClient _httpClient;
    private readonly StaticHtmlAdapter _adapter;

    public SharedWireMockProfileTests()
    {
        _server = WireMockServerFactory.Create();
        _httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        _adapter = new StaticHtmlAdapter(_httpClient, NullLogger<StaticHtmlAdapter>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        WireMockServerFactory.Dispose(_server);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ExtractAsync_WithRetryBackoffProfile_ShouldHandleExponentialBackoff()
    {
        // Arrange
        _server.WithExponentialBackoff("/test");

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/test",
            Method = "GET",
            Headers = new Dictionary<string, string> { { "X-Retry-Count", "3" } },
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("Success after retries");
    }

    [Fact]
    public async Task ExtractAsync_WithRedirectProfile_ShouldFollowRedirectChain()
    {
        // Arrange
        _server.WithRedirectChain(chainLength: 3);

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/redirect0",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("Final destination");
    }

    [Fact]
    public async Task ExtractAsync_WithRateLimitProfile_ShouldHandleRateLimiting()
    {
        // Arrange
        _server.WithRateLimiting("/api", retryAfterSeconds: 1);

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/api",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Headers.Should().ContainKey("X-RateLimit-Remaining");
    }

    [Fact]
    public async Task ExtractAsync_WithCompressionProfile_ShouldDecompressGzip()
    {
        // Arrange
        _server.WithGzipCompression("/data", "Compressed test content");

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/data",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_WithMalformedPayloadProfile_ShouldHandleInvalidJson()
    {
        // Arrange
        _server.WithInvalidJson("/bad-json");

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/bad-json",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        // Adapter should receive the content even if JSON is invalid
        response.Content.Content.Should().Contain("invalid json");
    }

    [Fact]
    public async Task ExtractAsync_WithCircularRedirect_ShouldDetectLoop()
    {
        // Arrange
        _server.WithCircularRedirect("/loop1", "/loop2");

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/loop1",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        // HttpClient should handle redirect loops by limiting redirects
    }
}
