using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

/// <summary>
/// Unit tests for ExponentialBackoffRetryPolicy.
/// </summary>
[Trait("Category", "Unit")]
public class ExponentialBackoffRetryPolicyTests
{
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ExponentialBackoffRetryPolicy(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOperation_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new RetryOptions();
        var policy = new ExponentialBackoffRetryPolicy(options);

        // Act
        var act = async () => await policy.ExecuteAsync<int>(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("operation");
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var options = new RetryOptions();
        var policy = new ExponentialBackoffRetryPolicy(options);
        var expectedResult = 42;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
            return expectedResult;
        });

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_WithHttpRequestException_RetriesUpToMaxRetries()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync<int>(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new HttpRequestException("Network error");
            });
        });

        // Should attempt: initial + 3 retries = 4 total attempts
        attemptCount.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeoutException_RetriesUpToMaxRetries()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync<int>(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new TimeoutException("Request timeout");
            });
        });

        // Should attempt: initial + 2 retries = 3 total attempts
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskCanceledException_RetriesWhenNotUserCancelled()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await policy.ExecuteAsync<int>(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new TaskCanceledException("Operation timeout");
            });
        });

        // Should retry TaskCanceledException when no cancellation token is set
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationTokenTriggered_DoesNotRetry()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await policy.ExecuteAsync<int>(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new TaskCanceledException("User cancelled");
            }, cts.Token);
        });

        // Should not retry when cancellation token is triggered
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonRetryableException_ThrowsImmediately()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync<int>(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new InvalidOperationException("Business logic error");
            });
        });

        // Should not retry non-retryable exceptions
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_AfterRetries_EventuallySucceeds()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        var attemptCount = 0;
        var expectedResult = 42;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;

            // Fail on first two attempts, succeed on third
            if (attemptCount < 3)
            {
                throw new HttpRequestException("Transient error");
            }

            return expectedResult;
        });

        // Assert
        result.Should().Be(expectedResult);
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_VerifiesExponentialBackoff_WithCorrectDelays()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(50),
            MaxDelay = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        List<DateTimeOffset> attemptTimestamps = [];

        // Act
        try
        {
            await policy.ExecuteAsync<int>(async () =>
            {
                attemptTimestamps.Add(DateTimeOffset.UtcNow);
                await Task.CompletedTask;
                throw new HttpRequestException("Network error");
            });
        }
        catch (HttpRequestException)
        {
            // Expected
        }

        // Assert
        attemptTimestamps.Should().HaveCount(4); // Initial + 3 retries

        // Verify exponential backoff delays (with some tolerance for timing variations)
        // Expected delays: 50ms, 100ms, 200ms
        var delay1 = attemptTimestamps[1] - attemptTimestamps[0];
        var delay2 = attemptTimestamps[2] - attemptTimestamps[1];
        var delay3 = attemptTimestamps[3] - attemptTimestamps[2];

        delay1.TotalMilliseconds.Should().BeGreaterOrEqualTo(45).And.BeLessThan(200);
        delay2.TotalMilliseconds.Should().BeGreaterOrEqualTo(95).And.BeLessThan(300);
        delay3.TotalMilliseconds.Should().BeGreaterOrEqualTo(195).And.BeLessThan(500);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsMaxDelay_WhenExponentialBackoffExceedsLimit()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(250),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        List<DateTimeOffset> attemptTimestamps = [];

        // Act
        try
        {
            await policy.ExecuteAsync<int>(async () =>
            {
                attemptTimestamps.Add(DateTimeOffset.UtcNow);
                await Task.CompletedTask;
                throw new HttpRequestException("Network error");
            });
        }
        catch (HttpRequestException)
        {
            // Expected
        }

        // Assert
        attemptTimestamps.Should().HaveCount(6); // Initial + 5 retries

        // Verify delays are capped at MaxDelay
        // Expected: 100ms, 200ms, 250ms (capped), 250ms (capped), 250ms (capped)
        var delay3 = attemptTimestamps[3] - attemptTimestamps[2];
        var delay4 = attemptTimestamps[4] - attemptTimestamps[3];
        var delay5 = attemptTimestamps[5] - attemptTimestamps[4];

        // All delays after the cap should be approximately MaxDelay
        delay3.TotalMilliseconds.Should().BeGreaterOrEqualTo(200).And.BeLessThan(400);
        delay4.TotalMilliseconds.Should().BeGreaterOrEqualTo(200).And.BeLessThan(400);
        delay5.TotalMilliseconds.Should().BeGreaterOrEqualTo(200).And.BeLessThan(400);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroMaxRetries_DoesNotRetry()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 0,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync<int>(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new HttpRequestException("Network error");
            });
        });

        // Should only attempt once with MaxRetries = 0
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentReturnTypes_WorksCorrectly()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };
        var policy = new ExponentialBackoffRetryPolicy(options);

        // Test with string
        var stringResult = await policy.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
            return "test";
        });
        stringResult.Should().Be("test");

        // Test with bool
        var boolResult = await policy.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
            return true;
        });
        boolResult.Should().BeTrue();

        // Test with complex object
        var person = new { Name = "John", Age = 30 };
        var objectResult = await policy.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
            return person;
        });
        objectResult.Should().BeEquivalentTo(person);
    }
}
