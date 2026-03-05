using Ghost.Resilience;
using Xunit;

namespace Ghost.Tests.Integration;

/// <summary>
/// Integration tests for Rock Solid 50K Scale implementation
/// </summary>
public class RockSolid50KIntegrationTests
{
    [Fact]
    public async Task CircuitBreaker_And_Retry_WorkTogether()
    {
        // Arrange
        ICircuitBreaker circuitBreaker = CircuitBreaker.CreateForLinkedIn();
        RetryPolicy retryPolicy = new RetryPolicy(new RetryPolicyOptions { MaxRetries = 3 });

        // Act - Simulate transient failure
        int attempts = 0;
        string result = await circuitBreaker.ExecuteAsync(async () =>
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                attempts++;
                if (attempts < 3)
                    throw new HttpRequestException("Transient error");
                return "success";
            }, ex => RetryableErrorClassifier.IsRetryable(ex));
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(3, attempts);
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public async Task CircuitBreaker_Opens_After_Failures()
    {
        // Arrange
        CircuitBreaker circuitBreaker = new CircuitBreaker("Test", new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            Timeout = TimeSpan.FromMilliseconds(100)
        });

        // Act - Exceed failure threshold
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync<string>(() => throw new Exception("Failure"));
            }
            catch
            {
                // Expected - intentionally causing failures to test circuit breaker threshold
            }
        }

        // Assert
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
    }

}
