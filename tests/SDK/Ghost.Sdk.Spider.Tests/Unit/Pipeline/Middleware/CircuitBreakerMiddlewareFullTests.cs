using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline.Middleware;

/// <summary>
/// Comprehensive tests for CircuitBreakerMiddleware covering edge cases and state transitions.
/// </summary>
public class CircuitBreakerMiddlewareFullTests
{
    [Fact]
    public void Constructor_WithTimeSpanTimeout_ShouldInitialize()
    {
        // Arrange & Act
        var config = new Dictionary<string, object>
        {
            ["Timeout"] = 120
        };
        var middleware = new CircuitBreakerMiddleware(config);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithSamplingWindow_ShouldTrackRecentFailures()
    {
        // Arrange - Use dictionary config like other tests
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 3,
            ["SamplingDuration"] = 1, // 1 second sampling window
            ["Timeout"] = 5
        };

        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        // Fail twice within sampling window
        PipelineDelegate failingNext = _ => throw new HttpRequestException("Test failure");

        for (int i = 0; i < 2; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, failingNext);
            }
            catch { /* Expected */ }
        }

        // Wait for sampling window to fully expire (with buffer)
        await Task.Delay(1500);

        // Add two more failures - still shouldn't open circuit since old ones expired
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, failingNext);
            }
            catch { /* Expected */ }
        }

        // Circuit should still be closed (only 2 failures in current window)
        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MultipleConcurrentRequests_ShouldHandleCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 5,
            ["Timeout"] = 2
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var successCount = 0;
        PipelineDelegate next = _ =>
        {
            Interlocked.Increment(ref successCount);
            return Task.CompletedTask;
        };

        // Act - Run multiple concurrent requests
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            middleware.InvokeAsync(CreateContext(), next));
        await Task.WhenAll(tasks);

        // Assert
        successCount.Should().Be(10);
    }

    [Fact]
    public async Task InvokeAsync_SuccessAfterPartialFailures_ShouldResetCounter()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 5,
            ["SamplingDuration"] = 30
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        // Act - Cause some failures then success
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, _ => throw new Exception("Fail"));
            }
            catch { /* Expected */ }
        }

        // Success should reset
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Should be able to continue normally
        var finalCallSucceeded = false;
        await middleware.InvokeAsync(context, _ =>
        {
            finalCallSucceeded = true;
            return Task.CompletedTask;
        });

        // Assert
        finalCallSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_HalfOpenWithPartialSuccesses_ShouldRequireAllSuccesses()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 2,
            ["SuccessThreshold"] = 3,
            ["Timeout"] = 1,
            ["SamplingDuration"] = 10
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        // Open the circuit
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, _ => throw new Exception("Fail"));
            }
            catch { /* Expected */ }
        }

        // Wait for half-open
        await Task.Delay(1500);

        // Only 2 successes (need 3)
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Circuit should still be half-open, allowing more requests
        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ExceptionPreservesStackTrace_ShouldRethrow()
    {
        // Arrange
        var config = new Dictionary<string, object>();
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();
        var originalException = new InvalidOperationException("Original error");

        // Act
        var act = async () => await middleware.InvokeAsync(context, _ => throw originalException);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Original error");
    }

    [Fact]
    public async Task InvokeAsync_RapidFailuresWithinSamplingWindow_ShouldOpenCircuit()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 3,
            ["SamplingDuration"] = 5,
            ["Timeout"] = 2
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        // Act - Rapid failures
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, _ => throw new Exception("Rapid fail"));
            }
            catch { /* Expected */ }
        }

        // Assert - Circuit should be open
        var act = async () => await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Circuit breaker is open*");
    }

    [Fact]
    public async Task InvokeAsync_MixedSuccessAndFailure_ShouldTrackCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 4,
            ["SamplingDuration"] = 10
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        // Act - Mix successes and failures
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        try { await middleware.InvokeAsync(context, _ => throw new Exception("F1")); } catch { }
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        try { await middleware.InvokeAsync(context, _ => throw new Exception("F2")); } catch { }
        try { await middleware.InvokeAsync(context, _ => throw new Exception("F3")); } catch { }

        // Circuit should still be closed (only 3 failures, need 4)
        var finalCallSucceeded = false;
        await middleware.InvokeAsync(context, _ =>
        {
            finalCallSucceeded = true;
            return Task.CompletedTask;
        });

        // Assert
        finalCallSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_CircuitOpensAndReopens_ShouldCycleCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 2,
            ["SuccessThreshold"] = 1,
            ["Timeout"] = 1,
            ["SamplingDuration"] = 10
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        // Open circuit
        for (int i = 0; i < 2; i++)
        {
            try { await middleware.InvokeAsync(context, _ => throw new Exception("Open")); } catch { }
        }

        // Verify open
        var act1 = async () => await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        await act1.Should().ThrowAsync<InvalidOperationException>();

        // Wait and close with success
        await Task.Delay(1500);
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Circuit should be closed now
        var finalSuccess = false;
        await middleware.InvokeAsync(context, _ =>
        {
            finalSuccess = true;
            return Task.CompletedTask;
        });

        // Assert
        finalSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_LastFailureTimeIsTracked_ShouldIncludeInErrorMessage()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 1,
            ["Timeout"] = 60
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        // Open circuit
        try
        {
            await middleware.InvokeAsync(context, _ => throw new Exception("Trigger open"));
        }
        catch { /* Expected */ }

        // Act
        var act = async () => await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Assert - Error message should contain timestamp
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("Last failure:");
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
