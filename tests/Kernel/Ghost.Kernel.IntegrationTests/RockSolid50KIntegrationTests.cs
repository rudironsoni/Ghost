using Ghost.Kernel.Caching;
using Ghost.Resilience;
using Ghost.Platform.LinkedIn;
using Microsoft.Extensions.Logging.Abstractions;
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
        var circuitBreaker = CircuitBreaker.CreateForLinkedIn();
        var retryPolicy = new RetryPolicy(new RetryPolicyOptions { MaxRetries = 3 });

        // Act - Simulate transient failure
        var attempts = 0;
        var result = await circuitBreaker.ExecuteAsync(async () =>
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
        var circuitBreaker = new CircuitBreaker("Test", new CircuitBreakerOptions
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
            catch { }
        }

        // Assert
        Assert.Equal(CircuitState.Open, circuitBreaker.State);

        // Should fail fast now
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            circuitBreaker.ExecuteAsync(() => Task.FromResult("test")));
    }

    [Fact]
    public async Task DLQ_Captures_Failed_Jobs()
    {
        // Arrange
        var dlq = new FileSystemDeadLetterQueue(Path.GetTempPath() + "/test-dlq");
        var job = new FailedScrapeJob
        {
            Platform = "LinkedIn",
            Query = "software engineer",
            Location = "remote",
            Error = "Rate limit exceeded"
        };

        // Act
        await dlq.EnqueueAsync(job);
        var jobs = await dlq.GetFailedJobsAsync(TimeSpan.FromMinutes(5));

        // Assert
        Assert.Single(jobs);
        Assert.Equal("LinkedIn", jobs[0].Platform);
        Assert.Equal("software engineer", jobs[0].Query);
    }

    [Fact]
    public async Task Cache_L1_L2_Fallback_Works()
    {
        // Arrange
        var cache = new MemoryFileHybridCache(
            Path.GetTempPath() + "/test-cache",
            NullLogger<MemoryFileHybridCache>.Instance);

        var jobs = new List<JobListing>
        {
            new() { Title = "Test Job", Company = "Test Co" }
        };

        // Act - Store in cache
        await cache.SetSearchResultsAsync("LinkedIn", "test", "remote", jobs, TimeSpan.FromMinutes(5));

        // Retrieve from cache
        var cached = await cache.GetSearchResultsAsync("LinkedIn", "test", "remote");

        // Assert
        Assert.NotNull(cached);
        Assert.Single(cached);
        Assert.Equal("Test Job", cached[0].Title);
    }

    [Theory]
    [InlineData("software engineer", "software%20engineer")]
    [InlineData("java OR python", "java%20OR%20python")]
    [InlineData("senior AND developer", "senior%20AND%20developer")]
    [InlineData("engineer NOT junior", "engineer%20NOT%20junior")]
    [InlineData("\"machine learning\"", "%22machine%20learning%22")]
    public void LinkedInQueryBuilder_EncodesCorrectly(string input, string expectedEncoded)
    {
        // Act
        var url = LinkedInQueryBuilder.BuildSearchUrl(input, "remote", 0);

        // Assert
        Assert.Contains($"keywords={expectedEncoded}", url);
    }

    [Fact]
    public void StripHtmlTags_RemovesAllTags()
    {
        // Arrange
        const string html = "<div><p>Test</p><script>alert('xss')</script></div>";

        // Act
        var result = HtmlSanitizer.StripHtmlTags(html);

        // Assert
        Assert.Equal("Test", result);
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
    }

    [Fact]
    public void StripHtmlTags_DecodesEntities()
    {
        // Arrange
        const string html = "&amp; &lt; &gt; &quot;";

        // Act
        var result = HtmlSanitizer.StripHtmlTags(html);

        // Assert
        Assert.Equal("& < > \"", result);
    }

    [Fact]
    public async Task SessionPool_AcquireAndRelease()
    {
        // This test requires a real GhostKernel
        // Skip if kernel not available
        var kernel = GetTestKernel();
        if (kernel == null)
        {
            Assert.Skip("GhostKernel not available");
            return;
        }

        // Arrange
        var pool = new LinkedInSessionPool(kernel, new LinkedInSessionPoolOptions
        {
            MaxSize = 2,
            WarmCount = 0
        }, NullLogger<LinkedInSessionPool>.Instance);

        // Act
        var session1 = await pool.AcquireAsync(CancellationToken.None);
        var metrics1 = pool.GetMetrics();

        pool.Release(session1);
        var metrics2 = pool.GetMetrics();

        // Assert
        Assert.Equal(1, metrics1.InUseCount);
        Assert.Equal(0, metrics1.AvailableCount);

        Assert.Equal(0, metrics2.InUseCount);
        Assert.Equal(1, metrics2.AvailableCount);
    }

    private GhostKernel? GetTestKernel()
    {
        // Factory method to get test kernel
        // Returns null if not available
        return null; // Placeholder
    }
}

// Placeholder classes for compilation
public class CircuitBreakerOpenException : Exception { }
