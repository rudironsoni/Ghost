using System.Diagnostics;
using System.Net.Http;
using FluentAssertions;
using Ghost.Resilience;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Resilience;

public class RetryPolicyTests : ReliabilityTestBase
{
    public RetryPolicyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task ExecuteAsyncSucceedsWithoutRetries()
    {
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        int result = await policy.ExecuteAsync(async () => await Task.FromResult(42), _ => true);

        result.Should().Be(42);
        policy.CurrentAttempt.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsyncRetriesUntilSuccess()
    {
        int attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(20),
            UseJitter = false
        });

        int result = await policy.ExecuteAsync(async () =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new HttpRequestException("transient");
            }

            return await Task.FromResult(7);
        }, RetryableErrorClassifier.IsRetryable);

        result.Should().Be(7);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsyncStopsAfterMaxRetries()
    {
        int attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        Func<Task> act = async () => await policy.ExecuteAsync(async () =>
        {
            attempts++;
            throw new HttpRequestException("transient");
        }, RetryableErrorClassifier.IsRetryable);

        await act.Should().ThrowAsync<HttpRequestException>();
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsyncDoesNotRetryForNonRetryableException()
    {
        int attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        Func<Task> act = async () => await policy.ExecuteAsync(async () =>
        {
            attempts++;
            throw new InvalidOperationException("parse error");
        }, RetryableErrorClassifier.IsRetryable);

        await act.Should().ThrowAsync<InvalidOperationException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsyncUsesZeroRetriesWhenMaxRetriesIsZero()
    {
        int attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 0,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        Func<Task> act = async () => await policy.ExecuteAsync(async () =>
        {
            attempts++;
            throw new HttpRequestException("transient");
        }, RetryableErrorClassifier.IsRetryable);

        await act.Should().ThrowAsync<HttpRequestException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsyncDelaysWithExponentialBackoffWithoutJitter()
    {
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(50),
            MaxDelay = TimeSpan.FromMilliseconds(1000),
            UseJitter = false
        });

        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await policy.ExecuteAsync(async () =>
            {
                throw new HttpRequestException("transient");
            }, RetryableErrorClassifier.IsRetryable));

        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task ExecuteAsyncThrowsWhenMaxDelayIsLessThanBaseDelay()
    {
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(20),
            UseJitter = false
        });

        Func<Task> act = async () => await policy.ExecuteAsync(async () =>
        {
            throw new HttpRequestException("transient");
        }, RetryableErrorClassifier.IsRetryable);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ExecuteHttpAsyncRetriesOnHttpRequestException()
    {
        int attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        Func<Task<HttpResponseMessage>> act = async () => await policy.ExecuteHttpAsync(async () =>
        {
            attempts++;
            throw new HttpRequestException("transient");
        });

        await act.Should().ThrowAsync<HttpRequestException>();
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteHttpAsyncRetriesOnRetryableStatusCode()
    {
        int attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        HttpResponseMessage response = await policy.ExecuteHttpAsync(async () =>
        {
            attempts++;
            return await Task.FromResult(new HttpResponseMessage(
                attempts == 1 ? System.Net.HttpStatusCode.ServiceUnavailable : System.Net.HttpStatusCode.OK));
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteHttpAsyncDoesNotRetryOnNonRetryableStatusCode()
    {
        int attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        HttpResponseMessage response = await policy.ExecuteHttpAsync(async () =>
        {
            attempts++;
            return await Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteHttpAsyncThrowsWhenActionReturnsNull()
    {
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 0,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        Func<Task<HttpResponseMessage>> act = async () => await policy.ExecuteHttpAsync(async () => await Task.FromResult<HttpResponseMessage>(null!));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsyncResetsCurrentAttemptAfterCompletion()
    {
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        await policy.ExecuteAsync(async () => await Task.FromResult(3), _ => true);

        policy.CurrentAttempt.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsyncPreservesAmbientAttemptValue()
    {
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        int outerAttempt = await policy.ExecuteAsync(async () =>
        {
            await policy.ExecuteAsync(async () => await Task.FromResult(1), _ => false);
            return policy.CurrentAttempt;
        }, _ => false);

        outerAttempt.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsyncInvalidOptionsThrow()
    {
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = -1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        Func<Task<int>> act = async () => await policy.ExecuteAsync(async () => await Task.FromResult(1), _ => true);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ExecuteAsyncNullArgumentsThrow()
    {
        var policy = new RetryPolicy();

        await Assert.ThrowsAsync<ArgumentNullException>(() => policy.ExecuteAsync<int>(null!, _ => true));
        await Assert.ThrowsAsync<ArgumentNullException>(() => policy.ExecuteAsync(async () => await Task.FromResult(1), null!));
    }

    [Fact]
    public async Task ExecuteAsyncAllowsRetryablePredicate()
    {
        int attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        });

        Func<Task> act = async () => await policy.ExecuteAsync(async () =>
        {
            attempts++;
            throw new HttpRequestException("transient");
        }, _ => false);

        await act.Should().ThrowAsync<HttpRequestException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsyncUsesMaxDelayWhenExceeded()
    {
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            UseJitter = true
        });

        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await policy.ExecuteAsync(async () =>
            {
                throw new HttpRequestException("transient");
            }, RetryableErrorClassifier.IsRetryable));

        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(100));
    }
}
