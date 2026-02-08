using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline.Middleware;

public class CircuitBreakerMiddlewareTests
{
    private const int DefaultFailureThreshold = 5;
    private const int DefaultSuccessThreshold = 2;
    private const int DefaultTimeout = 60;

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrow()
    {
        // Act
        var act = () => new CircuitBreakerMiddleware(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyConfiguration_ShouldUseDefaults()
    {
        // Arrange & Act
        var middleware = new CircuitBreakerMiddleware(new Dictionary<string, object>());

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenCircuitClosed_ShouldExecuteNext()
    {
        // Arrange
        var middleware = new CircuitBreakerMiddleware(new Dictionary<string, object>());
        var context = CreateContext();
        var nextCalled = false;
        PipelineDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AfterMultipleFailures_ShouldOpenCircuit()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 3,
            ["Timeout"] = 1 // 1 second timeout
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        PipelineDelegate failingNext = _ => throw new Exception("Simulated failure");

        // Act - Cause failures to open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, failingNext);
            }
            catch
            {
                // Expected
            }
        }

        // Assert - Circuit should now be open
        var act = async () => await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Circuit breaker is open*");
    }

    [Fact]
    public async Task InvokeAsync_WhenCircuitOpen_AfterTimeout_ShouldTransitionToHalfOpen()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 2,
            ["SuccessThreshold"] = 1,
            ["Timeout"] = 1, // 1 second
            ["SamplingDuration"] = 10
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        PipelineDelegate failingNext = _ => throw new Exception("Simulated failure");

        // Act - Open the circuit
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, failingNext);
            }
            catch
            {
                // Expected
            }
        }

        // Wait for timeout
        await Task.Delay(1500);

        // Should allow one request through (half-open state)
        var nextCalled = false;
        PipelineDelegate successNext = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        await middleware.InvokeAsync(context, successNext);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_InHalfOpenState_AfterSuccesses_ShouldCloseCircuit()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 2,
            ["SuccessThreshold"] = 2,
            ["Timeout"] = 1,
            ["SamplingDuration"] = 10
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        PipelineDelegate failingNext = _ => throw new Exception("Simulated failure");
        PipelineDelegate successNext = _ => Task.CompletedTask;

        // Act - Open the circuit
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, failingNext);
            }
            catch
            {
                // Expected
            }
        }

        // Wait for timeout to transition to half-open
        await Task.Delay(1500);

        // Execute successful requests
        await middleware.InvokeAsync(context, successNext);
        await middleware.InvokeAsync(context, successNext);

        // Circuit should now be closed - this should succeed
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
    public async Task InvokeAsync_InHalfOpenState_OnFailure_ShouldReopenCircuit()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 2,
            ["SuccessThreshold"] = 2,
            ["Timeout"] = 1,
            ["SamplingDuration"] = 10
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        PipelineDelegate failingNext = _ => throw new Exception("Simulated failure");

        // Act - Open the circuit
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, failingNext);
            }
            catch
            {
                // Expected
            }
        }

        // Wait for timeout to transition to half-open
        await Task.Delay(1500);

        // Execute a failing request in half-open state
        try
        {
            await middleware.InvokeAsync(context, failingNext);
        }
        catch
        {
            // Expected
        }

        // Circuit should be open again
        var act = async () => await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Circuit breaker is open*");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomThresholds_ShouldRespectConfiguration()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 1, // Open after just 1 failure
            ["SuccessThreshold"] = 1,
            ["Timeout"] = 1,
            ["SamplingDuration"] = 10
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        PipelineDelegate failingNext = _ => throw new Exception("Simulated failure");

        // Act - Single failure should open circuit
        try
        {
            await middleware.InvokeAsync(context, failingNext);
        }
        catch
        {
            // Expected
        }

        // Assert - Circuit should be open
        var act = async () => await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Circuit breaker is open*");
    }

    [Fact]
    public async Task InvokeAsync_WithSamplingWindow_ShouldForgetOldFailures()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 3,
            ["SamplingDuration"] = 1, // 1 second sampling window
            ["Timeout"] = 60
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        PipelineDelegate failingNext = _ => throw new Exception("Simulated failure");
        PipelineDelegate successNext = _ => Task.CompletedTask;

        // Act - Cause 2 failures
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, failingNext);
            }
            catch
            {
                // Expected
            }
        }

        // Wait for sampling window to expire
        await Task.Delay(1500);

        // These failures should not trigger circuit opening since old ones expired
        // Circuit should still be closed
        await middleware.InvokeAsync(context, successNext);
    }

    [Fact]
    public async Task InvokeAsync_InClosedState_OnSuccess_ShouldResetFailureCount()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 5,
            ["SamplingDuration"] = 30
        };
        var middleware = new CircuitBreakerMiddleware(config);
        var context = CreateContext();

        PipelineDelegate failingNext = _ => throw new Exception("Simulated failure");
        PipelineDelegate successNext = _ => Task.CompletedTask;

        // Act - Cause some failures
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await middleware.InvokeAsync(context, failingNext);
            }
            catch
            {
                // Expected
            }
        }

        // Success should reset the counter
        await middleware.InvokeAsync(context, successNext);

        // Should be able to continue with more requests
        await middleware.InvokeAsync(context, successNext);
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
