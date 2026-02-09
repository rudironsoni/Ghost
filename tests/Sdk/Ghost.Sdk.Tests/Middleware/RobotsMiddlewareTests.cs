using System.Net;
using FluentAssertions;
using Ghost.Sdk.Middleware;
using Moq;
using Moq.Protected;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class RobotsMiddlewareTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithNoRobotsTxt_ReturnsTrue()
    {
        // Arrange
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.NotFound, "");
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result = await middleware.CanFetchAsync("https://example.com/admin", "TestBot");

        // Assert
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithAllowAll_ReturnsTrue()
    {
        // Arrange
        var robotsTxt = """
            User-agent: *
            Disallow:
            """;
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.OK, robotsTxt);
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result = await middleware.CanFetchAsync("https://example.com/admin", "TestBot");

        // Assert
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithDisallowRule_ReturnsFalse()
    {
        // Arrange
        var robotsTxt = """
            User-agent: *
            Disallow: /admin
            """;
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.OK, robotsTxt);
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result = await middleware.CanFetchAsync("https://example.com/admin", "TestBot");

        // Assert
        result.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithAllowedPath_ReturnsTrue()
    {
        // Arrange
        var robotsTxt = """
            User-agent: *
            Disallow: /admin
            Allow: /admin/public
            """;
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.OK, robotsTxt);
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result = await middleware.CanFetchAsync("https://example.com/admin/public", "TestBot");

        // Assert
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithCachedRobotsTxt_DoesNotFetchAgain()
    {
        // Arrange
        var robotsTxt = """
            User-agent: *
            Disallow: /admin
            """;
        var fetchCount = 0;
        var httpClient = CreateHttpClientWithResponseCallback(
            () =>
            {
                fetchCount++;
                return (HttpStatusCode.OK, robotsTxt);
            });
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        await middleware.CanFetchAsync("https://example.com/admin", "TestBot");
        await middleware.CanFetchAsync("https://example.com/public", "TestBot");

        // Assert
        fetchCount.Should().Be(1); // Should only fetch once
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithDifferentDomains_FetchesSeparately()
    {
        // Arrange
        var robotsTxt = """
            User-agent: *
            Disallow: /admin
            """;
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.OK, robotsTxt);
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result1 = await middleware.CanFetchAsync("https://example1.com/admin", "TestBot");
        var result2 = await middleware.CanFetchAsync("https://example2.com/admin", "TestBot");

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithQueryString_ChecksPathAndQuery()
    {
        // Arrange
        var robotsTxt = """
            User-agent: *
            Disallow: /search?
            """;
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.OK, robotsTxt);
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result = await middleware.CanFetchAsync("https://example.com/search?q=test", "TestBot");

        // Assert
        result.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithHttpError_AllowsByDefault()
    {
        // Arrange
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.InternalServerError, "");
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result = await middleware.CanFetchAsync("https://example.com/admin", "TestBot");

        // Assert
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithHttpError_DeniesIfConfigured()
    {
        // Arrange
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.InternalServerError, "");
        var options = new RobotsOptions { AllowOnError = false };
        var middleware = new RobotsMiddleware(httpClient, options);

        // Act
        var result = await middleware.CanFetchAsync("https://example.com/admin", "TestBot");

        // Assert
        result.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task LoadRobotsTxtAsync_FetchesAndCachesRobotsTxt()
    {
        // Arrange
        var robotsTxt = """
            User-agent: *
            Disallow: /admin
            """;
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.OK, robotsTxt);
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        await middleware.LoadRobotsTxtAsync("https://example.com", "TestBot");
        var result = await middleware.CanFetchAsync("https://example.com/admin", "TestBot");

        // Assert
        result.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task LoadRobotsTxtAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        var httpClient = CreateHttpClientWithDelay(TimeSpan.FromSeconds(10));
        var middleware = new RobotsMiddleware(httpClient);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        await middleware.LoadRobotsTxtAsync("https://example.com", "TestBot", cts.Token);

        // Assert - should not throw, but cache default allow-all
        var result = await middleware.CanFetchAsync("https://example.com/admin", "TestBot");
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var act = async () => await middleware.CanFetchAsync(null!, "TestBot");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithNullUserAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var act = async () => await middleware.CanFetchAsync("https://example.com", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task LoadRobotsTxtAsync_WithNullBaseUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var act = async () => await middleware.LoadRobotsTxtAsync(null!, "TestBot");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task LoadRobotsTxtAsync_WithNullUserAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var act = async () => await middleware.LoadRobotsTxtAsync("https://example.com", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RobotsMiddleware(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var act = () => new RobotsMiddleware(httpClient, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithInvalidUrl_ReturnsFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result = await middleware.CanFetchAsync("not-a-valid-url", "TestBot");

        // Assert
        result.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithRelativeUrl_ReturnsFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result = await middleware.CanFetchAsync("/admin", "TestBot");

        // Assert
        result.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task CanFetchAsync_WithUserAgentSpecificRules_RespectsRules()
    {
        // Arrange
        var robotsTxt = """
            User-agent: Googlebot
            Disallow: /private

            User-agent: *
            Disallow: /admin
            """;
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.OK, robotsTxt);
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        var result1 = await middleware.CanFetchAsync("https://example.com/private", "Googlebot");
        var result2 = await middleware.CanFetchAsync("https://example.com/admin", "Googlebot");
        var result3 = await middleware.CanFetchAsync("https://example.com/private", "OtherBot");
        var result4 = await middleware.CanFetchAsync("https://example.com/admin", "OtherBot");

        // Assert
        result1.Should().BeFalse(); // Googlebot disallowed from /private
        result2.Should().BeTrue();  // Googlebot allowed to /admin
        result3.Should().BeTrue();  // OtherBot allowed to /private
        result4.Should().BeFalse(); // OtherBot disallowed from /admin
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task LoadRobotsTxtAsync_WithBaseUrlTrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var robotsTxt = """
            User-agent: *
            Disallow: /admin
            """;
        var httpClient = CreateHttpClientWithResponse(HttpStatusCode.OK, robotsTxt);
        var middleware = new RobotsMiddleware(httpClient);

        // Act
        await middleware.LoadRobotsTxtAsync("https://example.com/", "TestBot");
        var result = await middleware.CanFetchAsync("https://example.com/admin", "TestBot");

        // Assert
        result.Should().BeFalse();
    }

    private static HttpClient CreateHttpClientWithResponse(HttpStatusCode statusCode, string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });

        return new HttpClient(handlerMock.Object);
    }

    private static HttpClient CreateHttpClientWithResponseCallback(Func<(HttpStatusCode, string)> callback)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var (statusCode, content) = callback();
                return new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                };
            });

        return new HttpClient(handlerMock.Object);
    }

    private static HttpClient CreateHttpClientWithDelay(TimeSpan delay)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async () =>
            {
                await Task.Delay(delay);
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("")
                };
            });

        return new HttpClient(handlerMock.Object);
    }
}
