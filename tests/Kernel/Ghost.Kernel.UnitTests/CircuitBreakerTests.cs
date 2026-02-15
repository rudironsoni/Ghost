using Ghost.Resilience;
using Xunit;

namespace Ghost.Kernel.Tests;

public class CircuitBreakerTests
{
    [Fact]
    public async Task ExecuteAsyncAllowsSuccessInClosedState()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions());
        var result = await breaker.ExecuteAsync(() => Task.FromResult(42));
        Assert.Equal(42, result);
        Assert.Equal(CircuitState.Closed, breaker.State);
        var metrics = breaker.GetMetrics();
        Assert.Equal(1, metrics.SuccessCount);
    }

    [Fact]
    public async Task ExecuteAsyncOpensCircuitAfterFailureThreshold()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            Timeout = TimeSpan.FromMinutes(1),
            HalfOpenMaxAttempts = 1
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));

        Assert.Equal(CircuitState.Open, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsyncOpenCircuitRejectsUntilTimeout()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            Timeout = TimeSpan.FromMinutes(1),
            HalfOpenMaxAttempts = 1
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync(() => Task.FromResult(1)));
        Assert.Contains("open", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CircuitState.Open, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsyncTransitionsToHalfOpenAfterTimeout()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            Timeout = TimeSpan.FromMilliseconds(20),
            HalfOpenMaxAttempts = 1
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        await Task.Delay(30);

        var result = await breaker.ExecuteAsync(() => Task.FromResult(7));
        Assert.Equal(7, result);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsyncHalfOpenFailureReopensCircuit()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            Timeout = TimeSpan.FromMilliseconds(20),
            HalfOpenMaxAttempts = 2
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        await Task.Delay(30);

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        Assert.Equal(CircuitState.Open, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsyncHalfOpenRespectsAttemptLimit()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            Timeout = TimeSpan.FromMilliseconds(20),
            HalfOpenMaxAttempts = 1
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        await Task.Delay(30);

        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstAttempt = breaker.ExecuteAsync(() => gate.Task);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync(() => Task.FromResult(4)));
        Assert.Contains("open", exception.Message, StringComparison.OrdinalIgnoreCase);

        gate.SetResult(3);
        Assert.Equal(3, await firstAttempt);
    }

    [Fact]
    public async Task ExecuteAsyncClosedResetsConsecutiveFailuresOnSuccess()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            Timeout = TimeSpan.FromMinutes(1),
            HalfOpenMaxAttempts = 1
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        var result = await breaker.ExecuteAsync(() => Task.FromResult(5));
        Assert.Equal(5, result);
        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));

        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public void GetMetricsReturnsSnapshot()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions());
        var metrics = breaker.GetMetrics();
        Assert.Equal(0, metrics.FailureCount);
        Assert.Equal(0, metrics.SuccessCount);
        Assert.True(metrics.TimeInCurrentState >= TimeSpan.Zero);
    }

    [Fact]
    public async Task GetMetricsTracksFailureAndSuccessCounts()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            Timeout = TimeSpan.FromMinutes(1),
            HalfOpenMaxAttempts = 1
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        await breaker.ExecuteAsync(() => Task.FromResult(1));

        var metrics = breaker.GetMetrics();
        Assert.Equal(1, metrics.FailureCount);
        Assert.Equal(1, metrics.SuccessCount);
        Assert.NotEqual(DateTime.MinValue, metrics.LastFailure);
    }

    [Fact]
    public async Task StateChangedRaisesOnOpenAndClose()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            Timeout = TimeSpan.FromMilliseconds(20),
            HalfOpenMaxAttempts = 1
        });

        var transitions = new List<CircuitState>();
        breaker.StateChanged += (_, args) => transitions.Add(args.CurrentState);

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync<int>(() => throw new InvalidOperationException()));
        await Task.Delay(30);
        await breaker.ExecuteAsync(() => Task.FromResult(1));

        Assert.Contains(CircuitState.Open, transitions);
        Assert.Contains(CircuitState.HalfOpen, transitions);
        Assert.Contains(CircuitState.Closed, transitions);
    }

    [Fact]
    public void FactoryMethodsCreateConfiguredInstances()
    {
        var linkedIn = CircuitBreaker.CreateForLinkedIn();
        var indeed = CircuitBreaker.CreateForIndeed();
        var proxy = CircuitBreaker.CreateForProxy();

        Assert.Equal("LinkedIn", linkedIn.Platform);
        Assert.Equal("Indeed", indeed.Platform);
        Assert.Equal("Proxy", proxy.Platform);
    }

    [Fact]
    public void ConstructorValidatesOptions()
    {
        Assert.Throws<ArgumentException>(() => new CircuitBreaker(string.Empty, new CircuitBreakerOptions()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker("Test", new CircuitBreakerOptions { FailureThreshold = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker("Test", new CircuitBreakerOptions { HalfOpenMaxAttempts = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker("Test", new CircuitBreakerOptions { Timeout = TimeSpan.FromSeconds(-1) }));
    }

    [Fact]
    public async Task ExecuteAsyncThrowsWhenActionIsNull()
    {
        var breaker = new CircuitBreaker("Test", new CircuitBreakerOptions());
        await Assert.ThrowsAsync<ArgumentNullException>(() => breaker.ExecuteAsync<string>(null!));
    }
}
