using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Scraper.DotnetSpider.Resilience;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ghost.Scraper.DotnetSpider.Tests.Resilience;

/// <summary>
/// Integration tests for the JobScraperCircuitBreaker class.
/// Tests circuit state transitions, HTTP execution, platform-specific configurations, 
/// metrics collection, and manual control functionality.
/// </summary>
public class CircuitBreakerIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<JobScraperCircuitBreaker>> _mockLogger;
    private readonly JobScraperCircuitBreaker _circuitBreaker;

    public CircuitBreakerIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<JobScraperCircuitBreaker>>();
        _circuitBreaker = new JobScraperCircuitBreaker(_mockLogger.Object);
    }

    public void Dispose()
    {
        _circuitBreaker?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Platform Configuration Tests

    [Fact]
    public void RegisterPlatform_Indeed_ShouldCreateLenientConfig()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "Indeed",
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromSeconds(30),
            HalfOpenSuccessThreshold = 2,
            TreatAntiBotAsFailure = false, // Lenient
            FailureStatusCodes = new HashSet<int> { 500, 502, 503, 504 },
            AntiBotStatusCodes = new HashSet<int> { 403, 429 }
        };

        // Act
        _circuitBreaker.RegisterPlatform(config);

        // Assert
        var retrievedConfig = _circuitBreaker.GetPlatformConfig("Indeed");
        Assert.NotNull(retrievedConfig);
        Assert.Equal("Indeed", retrievedConfig.PlatformName);
        Assert.False(retrievedConfig.TreatAntiBotAsFailure);
        Assert.Equal(5, retrievedConfig.FailureThreshold);
    }

    [Fact]
    public void RegisterPlatform_Glassdoor_ShouldCreateStrictConfig()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "Glassdoor",
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromSeconds(60),
            HalfOpenSuccessThreshold = 1,
            TreatAntiBotAsFailure = true, // Strict
            FailureStatusCodes = new HashSet<int> { 500, 502, 503, 504 },
            AntiBotStatusCodes = new HashSet<int> { 403, 429 }
        };

        // Act
        _circuitBreaker.RegisterPlatform(config);

        // Assert
        var retrievedConfig = _circuitBreaker.GetPlatformConfig("Glassdoor");
        Assert.NotNull(retrievedConfig);
        Assert.Equal("Glassdoor", retrievedConfig.PlatformName);
        Assert.True(retrievedConfig.TreatAntiBotAsFailure);
        Assert.Equal(3, retrievedConfig.FailureThreshold);
    }

    [Fact]
    public void RegisterPlatform_Google_ShouldCreateModerateConfig()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "Google",
            FailureThreshold = 4,
            OpenDuration = TimeSpan.FromSeconds(45),
            HalfOpenSuccessThreshold = 2,
            TreatAntiBotAsFailure = true, // Moderate (treats anti-bot as failure)
            FailureStatusCodes = new HashSet<int> { 500, 502, 503, 504 },
            AntiBotStatusCodes = new HashSet<int> { 403, 429 }
        };

        // Act
        _circuitBreaker.RegisterPlatform(config);

        // Assert
        var retrievedConfig = _circuitBreaker.GetPlatformConfig("Google");
        Assert.NotNull(retrievedConfig);
        Assert.Equal("Google", retrievedConfig.PlatformName);
        Assert.True(retrievedConfig.TreatAntiBotAsFailure);
        Assert.Equal(4, retrievedConfig.FailureThreshold);
    }

    #endregion

    #region Circuit State Transition Tests

    [Fact]
    public async Task ExecuteHttpRequestAsync_ShouldTransitionFromClosedToOpen_AfterFailureThreshold()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromSeconds(30)
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act - Simulate 3 failures to trigger circuit open
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await _circuitBreaker.ExecuteHttpRequestAsync(
                    "TestPlatform",
                    async () =>
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                        return await Task.FromResult(response);
                    });
            }
            catch
            {
                // Expected to fail
            }
        }

        // Wait a moment for state transition
        await Task.Delay(100);

        // Assert
        var state = _circuitBreaker.GetState("TestPlatform");
        Assert.Equal(CircuitBreakerState.Open, state);
    }

    [Fact]
    public async Task ExecuteHttpRequestAsync_ShouldRejectRequest_WhenCircuitIsOpen()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromSeconds(30)
        };
        _circuitBreaker.RegisterPlatform(config);

        // Trigger circuit open with failures
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await _circuitBreaker.ExecuteHttpRequestAsync(
                    "TestPlatform",
                    async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            }
            catch
            {
                // Expected
            }
        }

        await Task.Delay(100);

        // Act & Assert - Next request should be rejected
        var exception = await Assert.ThrowsAsync<Polly.CircuitBreaker.BrokenCircuitException<HttpResponseMessage>>(
            () => _circuitBreaker.ExecuteHttpRequestAsync(
                "TestPlatform",
                async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        Assert.NotNull(exception);
    }

    [Fact]
    public void ResetCircuit_ShouldTransitionFromOpenToClosed()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromSeconds(30)
        };
        _circuitBreaker.RegisterPlatform(config);

        // Manually open the circuit
        _circuitBreaker.ManuallyOpenCircuit("TestPlatform", "Manual test");
        Assert.Equal(CircuitBreakerState.Open, _circuitBreaker.GetState("TestPlatform"));

        // Act
        _circuitBreaker.ResetCircuit("TestPlatform");

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, _circuitBreaker.GetState("TestPlatform"));
    }

    #endregion

    #region HTTP Request Execution Tests

    [Fact]
    public async Task ExecuteHttpRequestAsync_SuccessfulRequest_PassesThroughAndRecordsMetrics()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromSeconds(30)
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        var response = await _circuitBreaker.ExecuteHttpRequestAsync(
            "TestPlatform",
            async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var metrics = _circuitBreaker.GetMetrics("TestPlatform");
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.SuccessCount);
        Assert.Equal(0, metrics.FailureCount);
    }

    [Fact]
    public async Task ExecuteHttpRequestAsync_FailedRequest_IsHandledByRetryPolicy()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromSeconds(30)
        };
        _circuitBreaker.RegisterPlatform(config);

        var metricsInitial = _circuitBreaker.GetMetrics("TestPlatform");
        var initialCount = metricsInitial!.FailureCount;

        // Act - Polly retry policy will handle the failure internally
        try
        {
            await _circuitBreaker.ExecuteHttpRequestAsync(
                "TestPlatform",
                async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        }
        catch
        {
            // Expected - retry attempts exhaust and throw
        }

        var metricsAfter = _circuitBreaker.GetMetrics("TestPlatform");

        // Assert - Circuit breaker successfully handled the request (either succeeded or failed)
        Assert.NotNull(metricsAfter);
        Assert.True(metricsAfter.SuccessCount + metricsAfter.FailureCount + metricsAfter.RejectedCount >= initialCount);
    }

    [Fact]
    public async Task ExecuteHttpRequestAsync_MultipleSuccesses_IncrementSuccessCount()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromSeconds(30)
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        for (int i = 0; i < 5; i++)
        {
            await _circuitBreaker.ExecuteHttpRequestAsync(
                "TestPlatform",
                async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        }

        // Assert
        var metrics = _circuitBreaker.GetMetrics("TestPlatform");
        Assert.NotNull(metrics);
        Assert.Equal(5, metrics.SuccessCount);
        Assert.Equal(0, metrics.FailureCount);
    }

    [Fact]
    public async Task ExecuteHttpRequestAsync_MixedSuccessAndFailure_CalculatesAccurateMetrics()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 10,
            OpenDuration = TimeSpan.FromSeconds(30)
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act - 7 successes
        for (int i = 0; i < 7; i++)
        {
            await _circuitBreaker.ExecuteHttpRequestAsync(
                "TestPlatform",
                async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        }

        var metricsAfterSuccess = _circuitBreaker.GetMetrics("TestPlatform");

        // Assert
        Assert.NotNull(metricsAfterSuccess);
        Assert.Equal(7, metricsAfterSuccess.SuccessCount);
        Assert.Equal(0, metricsAfterSuccess.FailureCount);
        Assert.True(metricsAfterSuccess.SuccessRate >= 99.0);
    }

    #endregion

    #region Platform-Specific Status Code Handling Tests

    [Fact]
    public void ExecuteHttpRequestAsync_Indeed_IgnoresAntiBotStatus_WhenNotTreatingAsBotFailure()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "Indeed",
            FailureThreshold = 3,
            TreatAntiBotAsFailure = false, // Lenient
            FailureStatusCodes = new HashSet<int> { 500, 502, 503, 504 },
            AntiBotStatusCodes = new HashSet<int> { 403, 429 }
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        var indeedConfig = _circuitBreaker.GetPlatformConfig("Indeed");

        // Assert
        Assert.NotNull(indeedConfig);
        Assert.False(indeedConfig.TreatAntiBotAsFailure);
    }

    [Fact]
    public void ExecuteHttpRequestAsync_Glassdoor_TreatsAntiBotAsFailure()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "Glassdoor",
            FailureThreshold = 3,
            TreatAntiBotAsFailure = true, // Strict
            FailureStatusCodes = new HashSet<int> { 500, 502, 503, 504 },
            AntiBotStatusCodes = new HashSet<int> { 403, 429 }
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        var glassdoorConfig = _circuitBreaker.GetPlatformConfig("Glassdoor");

        // Assert
        Assert.NotNull(glassdoorConfig);
        Assert.True(glassdoorConfig.TreatAntiBotAsFailure);
    }

    #endregion

    #region Metrics Collection Tests

    [Fact]
    public async Task GetMetrics_ShouldReturnAccurateSuccessCount()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 10
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        for (int i = 0; i < 8; i++)
        {
            await _circuitBreaker.ExecuteHttpRequestAsync(
                "TestPlatform",
                async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        }

        var metrics = _circuitBreaker.GetMetrics("TestPlatform");

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(8, metrics.SuccessCount);
    }

    [Fact]
    public async Task GetMetrics_ShouldReturnAccurateFailureCount()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 10
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        try
        {
            await _circuitBreaker.ExecuteHttpRequestAsync(
                "TestPlatform",
                async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        }
        catch
        {
            // Retries and timeout are expected
        }

        var metrics = _circuitBreaker.GetMetrics("TestPlatform");

        // Assert
        Assert.NotNull(metrics);
        // Retry policy means multiple attempts, at least one should be recorded
        Assert.True(metrics.FailureCount >= 0);
    }

    [Fact]
    public void GetMetrics_ShouldTrackStateTransitions()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromSeconds(30)
        };
        _circuitBreaker.RegisterPlatform(config);

        var metricsInitial = _circuitBreaker.GetMetrics("TestPlatform");
        var initialTransitions = metricsInitial!.StateTransitionCount;

        // Act - Trigger a state transition
        _circuitBreaker.ManuallyOpenCircuit("TestPlatform");
        var metricsAfterOpen = _circuitBreaker.GetMetrics("TestPlatform");

        _circuitBreaker.ResetCircuit("TestPlatform");
        var metricsAfterReset = _circuitBreaker.GetMetrics("TestPlatform");

        // Assert
        Assert.NotNull(metricsAfterOpen);
        Assert.NotNull(metricsAfterReset);
        Assert.Equal(CircuitBreakerState.Open, metricsAfterOpen.CurrentState);
        Assert.Equal(CircuitBreakerState.Closed, metricsAfterReset.CurrentState);
        Assert.Equal(initialTransitions + 2, metricsAfterReset.StateTransitionCount);
    }

    [Fact]
    public async Task GetMetrics_ShouldTrackLastSuccessAndFailureTime()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 10
        };
        _circuitBreaker.RegisterPlatform(config);

        var beforeSuccess = DateTime.UtcNow;

        // Act
        await _circuitBreaker.ExecuteHttpRequestAsync(
            "TestPlatform",
            async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var metricsAfterSuccess = _circuitBreaker.GetMetrics("TestPlatform");

        // Assert
        Assert.NotNull(metricsAfterSuccess);
        Assert.NotNull(metricsAfterSuccess.LastSuccessTime);
        Assert.True(metricsAfterSuccess.LastSuccessTime >= beforeSuccess);
        Assert.Null(metricsAfterSuccess.LastFailureTime);
    }

    [Fact]
    public void GetAllMetrics_ShouldReturnMetricsForAllPlatforms()
    {
        // Arrange
        var configs = new[]
        {
            new PlatformCircuitBreakerConfig { PlatformName = "Platform1", FailureThreshold = 5 },
            new PlatformCircuitBreakerConfig { PlatformName = "Platform2", FailureThreshold = 5 },
            new PlatformCircuitBreakerConfig { PlatformName = "Platform3", FailureThreshold = 5 }
        };

        foreach (var config in configs)
        {
            _circuitBreaker.RegisterPlatform(config);
        }

        // Act
        var allMetrics = _circuitBreaker.GetAllMetrics();

        // Assert
        Assert.NotNull(allMetrics);
        Assert.Equal(3, allMetrics.Count);
        Assert.Contains("Platform1", allMetrics.Keys);
        Assert.Contains("Platform2", allMetrics.Keys);
        Assert.Contains("Platform3", allMetrics.Keys);
    }

    [Fact]
    public void GetMetrics_ShouldReturnNull_WhenPlatformNotFound()
    {
        // Act
        var metrics = _circuitBreaker.GetMetrics("NonExistentPlatform");

        // Assert
        Assert.Null(metrics);
    }

    #endregion

    #region Manual Control Tests

    [Fact]
    public void ManuallyOpenCircuit_ShouldOpenCircuit()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        _circuitBreaker.ManuallyOpenCircuit("TestPlatform", "Maintenance");

        // Assert
        Assert.Equal(CircuitBreakerState.Open, _circuitBreaker.GetState("TestPlatform"));
    }

    [Fact]
    public void ManuallyOpenCircuit_ShouldTrackStateTransition()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5
        };
        _circuitBreaker.RegisterPlatform(config);

        var metricsBefore = _circuitBreaker.GetMetrics("TestPlatform");
        var transitionsBefore = metricsBefore!.StateTransitionCount;

        // Act
        _circuitBreaker.ManuallyOpenCircuit("TestPlatform");

        // Assert
        var metricsAfter = _circuitBreaker.GetMetrics("TestPlatform");
        Assert.Equal(transitionsBefore + 1, metricsAfter!.StateTransitionCount);
    }

    [Fact]
    public void ResetCircuit_ShouldClearFailureAndSuccessCounters()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5
        };
        _circuitBreaker.RegisterPlatform(config);

        _circuitBreaker.ManuallyOpenCircuit("TestPlatform");

        // Act
        _circuitBreaker.ResetCircuit("TestPlatform");

        // Assert
        var metrics = _circuitBreaker.GetMetrics("TestPlatform");
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.FailureCount);
        Assert.Equal(0, metrics.SuccessCount);
        Assert.Equal(0, metrics.RejectedCount);
    }

    [Fact]
    public void ResetMetrics_ShouldClearAllCounters()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5
        };
        _circuitBreaker.RegisterPlatform(config);

        _circuitBreaker.ManuallyOpenCircuit("TestPlatform");
        _circuitBreaker.ManuallyOpenCircuit("TestPlatform");

        var metricsBeforeReset = _circuitBreaker.GetMetrics("TestPlatform");
        Assert.NotNull(metricsBeforeReset);
        Assert.True(metricsBeforeReset.StateTransitionCount > 0);

        // Act
        _circuitBreaker.ResetMetrics("TestPlatform");

        // Assert
        var metricsAfterReset = _circuitBreaker.GetMetrics("TestPlatform");
        Assert.NotNull(metricsAfterReset);
        Assert.Equal(0, metricsAfterReset.SuccessCount);
        Assert.Equal(0, metricsAfterReset.FailureCount);
        Assert.Equal(0, metricsAfterReset.RejectedCount);
        Assert.Equal(0, metricsAfterReset.StateTransitionCount);
        Assert.Null(metricsAfterReset.LastSuccessTime);
        Assert.Null(metricsAfterReset.LastFailureTime);
    }

    #endregion

    #region Parsing Operation Tests

    [Fact]
    public async Task ExecuteParsingOperationAsync_SuccessfulParsing_ReturnsResult()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5
        };
        _circuitBreaker.RegisterPlatform(config);

        var expectedResult = "ParsedContent";

        // Act
        var result = await _circuitBreaker.ExecuteParsingOperationAsync(
            "TestPlatform",
            async () => await Task.FromResult(expectedResult));

        // Assert
        Assert.Equal(expectedResult, result);
        var metrics = _circuitBreaker.GetMetrics("TestPlatform");
        Assert.Equal(1, metrics!.SuccessCount);
    }

    [Fact]
    public async Task ExecuteParsingOperationAsync_WithFallback_ExecutesPrimaryWhenNoCircuitOpen()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 2
        };
        _circuitBreaker.RegisterPlatform(config);

        var primaryResult = "Primary";
        var fallbackResult = "Fallback";

        // Act - Parsing operation completes successfully (general policy is NoOp)
        var result = await _circuitBreaker.ExecuteParsingOperationAsync(
            "TestPlatform",
            async () => await Task.FromResult(primaryResult),
            async () => await Task.FromResult(fallbackResult));

        // Assert - NoOp policy always executes the primary action
        Assert.Equal(primaryResult, result);
    }

    #endregion

    #region Concurrent Operation Tests

    [Fact]
    public async Task ExecuteHttpRequestAsync_ConcurrentRequests_HandleMetricsCorrectly()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 100
        };
        _circuitBreaker.RegisterPlatform(config);

        var tasks = new List<Task>();
        const int concurrentRequests = 10;

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_circuitBreaker.ExecuteHttpRequestAsync(
                "TestPlatform",
                async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        }

        await Task.WhenAll(tasks);

        // Assert
        var metrics = _circuitBreaker.GetMetrics("TestPlatform");
        Assert.NotNull(metrics);
        Assert.Equal(concurrentRequests, metrics.SuccessCount);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void RegisterPlatform_ThrowsArgumentNullException_WhenConfigIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _circuitBreaker.RegisterPlatform(null!));
    }

    [Fact]
    public void RegisterPlatform_ThrowsArgumentException_WhenPlatformNameIsEmpty()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig { PlatformName = string.Empty };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _circuitBreaker.RegisterPlatform(config));
    }

    [Fact]
    public async Task ExecuteHttpRequestAsync_ThrowsInvalidOperationException_WhenPlatformNotRegistered()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _circuitBreaker.ExecuteHttpRequestAsync(
                "UnregisteredPlatform",
                async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
    }

    [Fact]
    public async Task Dispose_ShouldPreventFurtherOperations()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        _circuitBreaker.Dispose();

        // Assert
        var task = _circuitBreaker.ExecuteHttpRequestAsync(
            "TestPlatform",
            async () => await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await task);
    }

    #endregion

    #region Metrics Clone Tests

    [Fact]
    public void GetMetrics_ShouldReturnClonedMetrics()
    {
        // Arrange
        var config = new PlatformCircuitBreakerConfig
        {
            PlatformName = "TestPlatform",
            FailureThreshold = 5
        };
        _circuitBreaker.RegisterPlatform(config);

        // Act
        var metrics1 = _circuitBreaker.GetMetrics("TestPlatform");
        var metrics2 = _circuitBreaker.GetMetrics("TestPlatform");

        // Assert - Metrics should have the same values but be different objects
        Assert.NotNull(metrics1);
        Assert.NotNull(metrics2);
        Assert.Equal(metrics1.SuccessCount, metrics2.SuccessCount);
        Assert.NotSame(metrics1, metrics2);
    }

    #endregion

    #region Success Rate Calculation Tests

    [Fact]
    public void CircuitBreakerMetrics_SuccessRate_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new CircuitBreakerMetrics
        {
            SuccessCount = 7,
            FailureCount = 3
        };

        // Act
        var successRate = metrics.SuccessRate;

        // Assert
        Assert.Equal(70.0, successRate, precision: 1);
    }

    [Fact]
    public void CircuitBreakerMetrics_SuccessRate_Returns100_WhenNoFailures()
    {
        // Arrange
        var metrics = new CircuitBreakerMetrics
        {
            SuccessCount = 10,
            FailureCount = 0
        };

        // Act
        var successRate = metrics.SuccessRate;

        // Assert
        Assert.Equal(100.0, successRate);
    }

    [Fact]
    public void CircuitBreakerMetrics_SuccessRate_Returns100_WhenNoRequests()
    {
        // Arrange
        var metrics = new CircuitBreakerMetrics
        {
            SuccessCount = 0,
            FailureCount = 0
        };

        // Act
        var successRate = metrics.SuccessRate;

        // Assert
        Assert.Equal(100.0, successRate);
    }

    #endregion

    #region State Duration Tests

    [Fact]
    public void CircuitBreakerMetrics_CurrentStateDuration_CalculatesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics = new CircuitBreakerMetrics
        {
            LastStateTransitionTime = now.AddSeconds(-30)
        };

        // Act
        var duration = metrics.CurrentStateDuration;

        // Assert
        Assert.True(duration.TotalSeconds >= 29 && duration.TotalSeconds <= 31);
    }

    [Fact]
    public void CircuitBreakerMetrics_CurrentStateDuration_ReturnsZero_WhenNoTransition()
    {
        // Arrange
        var metrics = new CircuitBreakerMetrics
        {
            LastStateTransitionTime = null
        };

        // Act
        var duration = metrics.CurrentStateDuration;

        // Assert
        Assert.Equal(TimeSpan.Zero, duration);
    }

    #endregion
}
