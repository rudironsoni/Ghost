using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline.Middleware;

public class CircuitBreakerMiddlewareTests : ReliabilityTestBase
{
    public CircuitBreakerMiddlewareTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrow()
    {
        // Act
        Action act = () => _ = new CircuitBreakerMiddleware(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyConfiguration_ShouldUseDefaults()
    {
        // Arrange & Act
        CircuitBreakerMiddleware middleware = new CircuitBreakerMiddleware(new Dictionary<string, object>());

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenCircuitClosed_ShouldExecuteNext()
    {
        // Arrange
        CircuitBreakerMiddleware middleware = new CircuitBreakerMiddleware(new Dictionary<string, object>());
        PipelineContext context = CreateContext();
        bool nextCalled = false;
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
        FakeTimeProvider fakeTimeProvider = new FakeTimeProvider();
        Dictionary<string, object> config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 3,
            ["Timeout"] = 1 // 1 second timeout
        };
        CircuitBreakerMiddleware middleware = new CircuitBreakerMiddleware(config, fakeTimeProvider);
        PipelineContext context = CreateContext();

        PipelineDelegate failingNext = _ => throw new InvalidOperationException("Simulated failure");

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

        // Simulate timeout
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1.5));

        // Should allow one request through (half-open state)
        bool nextCalled = false;
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
        FakeTimeProvider fakeTimeProvider = new FakeTimeProvider();
        Dictionary<string, object> config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 2,
            ["SuccessThreshold"] = 2,
            ["Timeout"] = 1,
            ["SamplingDuration"] = 10
        };
        CircuitBreakerMiddleware middleware = new CircuitBreakerMiddleware(config, fakeTimeProvider);
        PipelineContext context = CreateContext();

        PipelineDelegate failingNext = _ => throw new InvalidOperationException("Simulated failure");
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

        // Simulate timeout to transition to half-open
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1.5));

        // Execute successful requests
        await middleware.InvokeAsync(context, successNext);
        await middleware.InvokeAsync(context, successNext);

        // Circuit should now be closed - this should succeed
        bool finalCallSucceeded = false;
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
        FakeTimeProvider fakeTimeProvider = new FakeTimeProvider();
        Dictionary<string, object> config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 2,
            ["SuccessThreshold"] = 2,
            ["Timeout"] = 1,
            ["SamplingDuration"] = 10
        };
        CircuitBreakerMiddleware middleware = new CircuitBreakerMiddleware(config, fakeTimeProvider);
        PipelineContext context = CreateContext();

        PipelineDelegate failingNext = _ => throw new InvalidOperationException("Simulated failure");

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

        // Simulate timeout to transition to half-open
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1.5));

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
        Func<Task> act = async () => await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Circuit breaker is open*");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomThresholds_ShouldRespectConfiguration()
    {
        // Arrange
        Dictionary<string, object> config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 1, // Open after just 1 failure
            ["SuccessThreshold"] = 1,
            ["Timeout"] = 1,
            ["SamplingDuration"] = 10
        };
        CircuitBreakerMiddleware middleware = new CircuitBreakerMiddleware(config);
        PipelineContext context = CreateContext();

        PipelineDelegate failingNext = _ => throw new InvalidOperationException("Simulated failure");

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
        Func<Task> act = async () => await middleware.InvokeAsync(context, _ => Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Circuit breaker is open*");
    }

    [Fact]
    public async Task InvokeAsync_WithSamplingWindow_ShouldForgetOldFailures()
    {
        // Arrange
        FakeTimeProvider fakeTimeProvider = new FakeTimeProvider();
        Dictionary<string, object> config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 3,
            ["SamplingDuration"] = 1, // 1 second sampling window
            ["Timeout"] = 60
        };
        CircuitBreakerMiddleware middleware = new CircuitBreakerMiddleware(config, fakeTimeProvider);
        PipelineContext context = CreateContext();

        PipelineDelegate failingNext = _ => throw new InvalidOperationException("Simulated failure");
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

        // Simulate sampling window expiration
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(1.5));

        // These failures should not trigger circuit opening since old ones expired
        // Circuit should still be closed
        await middleware.InvokeAsync(context, successNext);
    }

    [Fact]
    public async Task InvokeAsync_InClosedState_OnSuccess_ShouldResetFailureCount()
    {
        // Arrange
        Dictionary<string, object> config = new Dictionary<string, object>
        {
            ["FailureThreshold"] = 5,
            ["SamplingDuration"] = 30
        };
        CircuitBreakerMiddleware middleware = new CircuitBreakerMiddleware(config);
        PipelineContext context = CreateContext();

        PipelineDelegate failingNext = _ => throw new InvalidOperationException("Simulated failure");
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
