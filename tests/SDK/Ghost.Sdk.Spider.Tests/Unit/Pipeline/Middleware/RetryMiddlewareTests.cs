using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline.Middleware;

public class RetryMiddlewareTests
{
    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrow()
    {
        // Act
        var act = () => new RetryMiddleware(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyConfiguration_ShouldUseDefaults()
    {
        // Arrange & Act
        var middleware = new RetryMiddleware(new Dictionary<string, object>());

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_OnFirstSuccess_ShouldNotRetry()
    {
        // Arrange
        var middleware = new RetryMiddleware(new Dictionary<string, object>());
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_OnTransientFailure_ShouldRetry()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MaxRetries"] = 2,
            ["InitialDelayMs"] = 10, // Short delay for testing
            ["UseJitter"] = false
        };
        var middleware = new RetryMiddleware(config);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            if (callCount < 2)
            {
                throw new HttpRequestException("Temporary error");
            }
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task InvokeAsync_WhenAllRetriesFail_ShouldThrowAggregateException()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MaxRetries"] = 2,
            ["InitialDelayMs"] = 10,
            ["UseJitter"] = false
        };
        var middleware = new RetryMiddleware(config);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            throw new HttpRequestException($"Error #{callCount}");
        };

        // Act
        var act = async () => await middleware.InvokeAsync(context, next);

        // Assert
        await act.Should().ThrowAsync<AggregateException>()
            .WithMessage("*failed after 3 attempts*");
        callCount.Should().Be(3); // Initial + 2 retries
    }

    [Fact]
    public async Task InvokeAsync_OnCancellation_ShouldNotRetry()
    {
        // Arrange
        var middleware = new RetryMiddleware(new Dictionary<string, object>());
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            throw new OperationCanceledException("Cancelled");
        };

        // Act
        var act = async () => await middleware.InvokeAsync(context, next);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_OnTimeoutException_WithRetryOnTimeout_ShouldRetry()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MaxRetries"] = 1,
            ["InitialDelayMs"] = 10,
            ["RetryOnTimeout"] = true,
            ["UseJitter"] = false
        };
        var middleware = new RetryMiddleware(config);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new TimeoutException("Request timed out");
            }
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task InvokeAsync_OnInvalidOperationException_ShouldNotRetry()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MaxRetries"] = 2,
            ["InitialDelayMs"] = 10
        };
        var middleware = new RetryMiddleware(config);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            throw new InvalidOperationException("Invalid state");
        };

        // Act
        var act = async () => await middleware.InvokeAsync(context, next);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(1); // No retries
    }

    [Fact]
    public async Task InvokeAsync_WithCustomMaxRetries_ShouldRespect()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MaxRetries"] = 5,
            ["InitialDelayMs"] = 10,
            ["UseJitter"] = false
        };
        var middleware = new RetryMiddleware(config);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            if (callCount < 4)
            {
                throw new HttpRequestException("Temporary error");
            }
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert
        callCount.Should().Be(4);
    }

    [Fact]
    public async Task InvokeAsync_WithBackoffMultiplier_ShouldIncreaseDelay()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MaxRetries"] = 2,
            ["InitialDelayMs"] = 50,
            ["BackoffMultiplier"] = 3.0,
            ["UseJitter"] = false
        };
        var middleware = new RetryMiddleware(config);
        var context = CreateContext();
        var callCount = 0;
        var startTime = DateTime.UtcNow;
        var delays = new List<TimeSpan>();

        PipelineDelegate next = _ =>
        {
            if (callCount > 0)
            {
                var elapsed = DateTime.UtcNow - startTime;
                delays.Add(elapsed);
            }
            startTime = DateTime.UtcNow;
            callCount++;
            if (callCount < 3)
            {
                throw new HttpRequestException("Temporary error");
            }
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert
        callCount.Should().Be(3);
        delays.Should().HaveCount(2);
        // Second delay should be roughly 3x the first (50ms, 150ms)
        delays[1].Should().BeGreaterThan(delays[0]);
    }

    [Fact]
    public async Task InvokeAsync_WithMaxDelay_ShouldCapDelay()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MaxRetries"] = 3,
            ["InitialDelayMs"] = 1000,
            ["MaxDelayMs"] = 2000, // Cap at 2 seconds
            ["BackoffMultiplier"] = 10.0, // Very high multiplier
            ["UseJitter"] = false
        };
        var middleware = new RetryMiddleware(config);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            throw new HttpRequestException("Temporary error");
        };

        // Act
        var startTime = DateTime.UtcNow;
        try
        {
            await middleware.InvokeAsync(context, next);
        }
        catch
        {
            // Expected
        }
        var totalTime = DateTime.UtcNow - startTime;

        // Assert
        // Total time should be less than if delays weren't capped
        // Expected: ~1s + ~2s + ~2s = ~5s (with capping)
        // Without capping: ~1s + ~10s + ~100s = much longer
        totalTime.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task InvokeAsync_OnSuccess_ShouldIncrementRetryCount()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MaxRetries"] = 2,
            ["InitialDelayMs"] = 10,
            ["UseJitter"] = false
        };
        var middleware = new RetryMiddleware(config);
        var context = CreateContext();
        var callCount = 0;

        PipelineDelegate next = _ =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HttpRequestException("Temporary error");
            }
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert - The middleware increments once per retry, plus StateBox may increment
        // The exact count depends on how many times IncrementRetryCount is called
        context.StateBox.Should().NotBeNull();
        context.StateBox!.RetryCount.Should().BeGreaterOrEqualTo(1);
    }

    private static PipelineContext CreateContext()
    {
        return new PipelineContext
        {
            Request = new Request
            {
                RequestId = Guid.NewGuid().ToString(),
                Url = "https://example.com",
                Method = "GET",
                Timeout = TimeSpan.FromSeconds(30)
            },
            StateBox = new SpiderStateBox(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };
    }
}
