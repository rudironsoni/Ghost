using FluentAssertions;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using Ghost.Sdk.Spider.Adapters.Contracts;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline.Middleware;

[TestFixture]
public class RateLimitMiddlewareTests
{
    [Test]
    public async Task InvokeAsync_WithinLimit_ShouldExecuteImmediately()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["Capacity"] = 10,
            ["TokensPerSecond"] = 10.0,
            ["PerDomain"] = false,
            ["WaitWhenExceeded"] = false
        };
        var middleware = new RateLimitMiddleware(config);
        var stateBox = new SpiderStateBox();
        
        var request = new Request
        {
            RequestId = "test-request",
            Url = "https://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        var context = new PipelineContext
        {
            StateBox = stateBox,
            RequestId = 1,
            Request = request,
            CancellationToken = CancellationToken.None
        };

        var nextCalled = false;
        PipelineDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        var start = DateTimeOffset.UtcNow;
        await middleware.InvokeAsync(context, next);
        var elapsed = DateTimeOffset.UtcNow - start;

        // Assert
        nextCalled.Should().BeTrue();
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1)); // Should be almost instant
    }

    [Test]
    public async Task InvokeAsync_ExceedingLimit_ShouldWait()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["Capacity"] = 1,
            ["TokensPerSecond"] = 0.5, // 1 request per 2 seconds
            ["PerDomain"] = false,
            ["WaitWhenExceeded"] = true
        };
        var middleware = new RateLimitMiddleware(config);

        // Execute first request (uses the token)
        var stateBox = new SpiderStateBox();
        var request1 = new Request
        {
            RequestId = "request-1",
            Url = "https://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var context1 = new PipelineContext
        {
            StateBox = stateBox,
            RequestId = 1,
            Request = request1,
            CancellationToken = CancellationToken.None
        };

        await middleware.InvokeAsync(context1, ctx => Task.CompletedTask);

        // Try second request immediately (should wait for token)
        var request2 = new Request
        {
            RequestId = "request-2",
            Url = "https://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var context2 = new PipelineContext
        {
            StateBox = stateBox,
            RequestId = 2,
            Request = request2,
            CancellationToken = CancellationToken.None
        };

        // Act
        var start = DateTimeOffset.UtcNow;
        await middleware.InvokeAsync(context2, ctx => Task.CompletedTask);
        var elapsed = DateTimeOffset.UtcNow - start;

        // Assert
        elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(500)); // Should wait
    }

    [Test]
    public void InvokeAsync_ExceedingLimitNoWait_ShouldThrow()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["Capacity"] = 1,
            ["TokensPerSecond"] = 0.5,
            ["PerDomain"] = false,
            ["WaitWhenExceeded"] = false
        };
        var middleware = new RateLimitMiddleware(config);

        var stateBox = new SpiderStateBox();
        var request1 = new Request
        {
            RequestId = "request-1",
            Url = "https://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var context1 = new PipelineContext
        {
            StateBox = stateBox,
            RequestId = 1,
            Request = request1,
            CancellationToken = CancellationToken.None
        };

        // Execute first request
        middleware.InvokeAsync(context1, ctx => Task.CompletedTask).Wait();

        var request2 = new Request
        {
            RequestId = "request-2",
            Url = "https://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var context2 = new PipelineContext
        {
            StateBox = stateBox,
            RequestId = 2,
            Request = request2,
            CancellationToken = CancellationToken.None
        };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await middleware.InvokeAsync(context2, ctx => Task.CompletedTask));
    }

    [Test]
    public async Task InvokeAsync_PerDomain_ShouldRateLimitSeparately()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["Capacity"] = 1,
            ["TokensPerSecond"] = 0.5,
            ["PerDomain"] = true,
            ["WaitWhenExceeded"] = true
        };
        var middleware = new RateLimitMiddleware(config);

        // Request to domain1
        var stateBox = new SpiderStateBox();
        var request1 = new Request
        {
            RequestId = "request-1",
            Url = "https://domain1.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var context1 = new PipelineContext
        {
            StateBox = stateBox,
            RequestId = 1,
            Request = request1,
            CancellationToken = CancellationToken.None
        };

        await middleware.InvokeAsync(context1, ctx => Task.CompletedTask);

        // Immediate request to domain2 (should not wait)
        var request2 = new Request
        {
            RequestId = "request-2",
            Url = "https://domain2.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var context2 = new PipelineContext
        {
            StateBox = stateBox,
            RequestId = 2,
            Request = request2,
            CancellationToken = CancellationToken.None
        };

        // Act
        var start = DateTimeOffset.UtcNow;
        await middleware.InvokeAsync(context2, ctx => Task.CompletedTask);
        var elapsed = DateTimeOffset.UtcNow - start;

        // Assert
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500)); // Should be fast (different domain)
    }

    [Test]
    public void Constructor_WithNullConfiguration_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RateLimitMiddleware(null!));
    }

    [Test]
    public void Constructor_WithEmptyConfiguration_ShouldUseDefaults()
    {
        // Arrange
        var config = new Dictionary<string, object>();

        // Act
        var middleware = new RateLimitMiddleware(config);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Test]
    public async Task InvokeAsync_WithCustomCapacity_ShouldRespectCapacity()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["Capacity"] = 3,
            ["TokensPerSecond"] = 100.0, // High rate
            ["PerDomain"] = false,
            ["WaitWhenExceeded"] = false
        };
        var middleware = new RateLimitMiddleware(config);

        var stateBox = new SpiderStateBox();
        
        // Act - Should allow 3 requests in burst
        for (int i = 0; i < 3; i++)
        {
            var request = new Request
            {
                RequestId = $"request-{i}",
                Url = "https://example.com",
                Method = "GET",
                Timeout = TimeSpan.FromSeconds(30)
            };
            var context = new PipelineContext
            {
                StateBox = stateBox,
                RequestId = i + 1,
                Request = request,
                CancellationToken = CancellationToken.None
            };

            await middleware.InvokeAsync(context, ctx => Task.CompletedTask);
        }

        // 4th request should fail
        var request4 = new Request
        {
            RequestId = "request-4",
            Url = "https://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var context4 = new PipelineContext
        {
            StateBox = stateBox,
            RequestId = 4,
            Request = request4,
            CancellationToken = CancellationToken.None
        };

        // Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await middleware.InvokeAsync(context4, ctx => Task.CompletedTask));
    }
}
