using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ghost.Scraper.DotnetSpider.Monitoring;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ghost.Scraper.DotnetSpider.Tests.Monitoring;

public class MonitoringServiceIntegrationTests
{
    private readonly Mock<ILogger<JobScraperMonitoringService>> _mockLogger;
    private readonly JobScraperMonitoringService _monitoringService;

    private const string IndeedPlatform = "Indeed";
    private const string GlassdoorPlatform = "Glassdoor";
    private const string GooglePlatform = "Google";

    public MonitoringServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<JobScraperMonitoringService>>();
        _monitoringService = new JobScraperMonitoringService(_mockLogger.Object);
    }

    #region Request Recording Tests

    [Fact]
    public void RecordRequest_SuccessfulRequest_ShouldRecordCorrectly()
    {
        const long latency = 100;
        _monitoringService.RecordRequest(IndeedPlatform, success: true, latency);

        var health = _monitoringService.GetPlatformHealth(IndeedPlatform);
        Assert.NotNull(health);
        Assert.Equal(IndeedPlatform, health.PlatformName);
        Assert.Equal(100, health.SuccessRate);
        Assert.Equal("Healthy", health.Status.ToString());
    }

    [Fact]
    public void RecordRequest_FailedRequest_ShouldRecordCorrectly()
    {
        const long latency = 50;
        _monitoringService.RecordRequest(GlassdoorPlatform, success: false, latency);

        var health = _monitoringService.GetPlatformHealth(GlassdoorPlatform);
        Assert.NotNull(health);
        Assert.Equal(GlassdoorPlatform, health.PlatformName);
        Assert.Equal(0, health.SuccessRate);
        Assert.Equal("Unhealthy", health.Status.ToString());
        Assert.Equal(1, health.ErrorCount);
    }

    [Fact]
    public void RecordRequest_WithLatency_ShouldCalculateAverageLatency()
    {
        var latencies = new[] { 100L, 200L, 300L };
        foreach (var latency in latencies)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: true, latency);
        }

        var metrics = _monitoringService.GetCurrentMetrics();
        Assert.NotNull(metrics);
        Assert.True(metrics.PerPlatformMetrics.ContainsKey(GooglePlatform));
        var platformMetrics = metrics.PerPlatformMetrics[GooglePlatform];
        Assert.Equal(200, platformMetrics.AverageLatencyMs);
    }

    [Fact]
    public void RecordRequest_WithErrorCategory_ShouldRecordCategoryAndLog()
    {
        const string errorCategory = "TimeoutError";
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 150, errorCategory);

        var metrics = _monitoringService.GetCurrentMetrics();
        Assert.True(metrics.PerPlatformMetrics.ContainsKey(IndeedPlatform));
        var platformMetrics = metrics.PerPlatformMetrics[IndeedPlatform];
        Assert.True(platformMetrics.ErrorCategories.ContainsKey(errorCategory));
        Assert.Equal(1, platformMetrics.ErrorCategories[errorCategory]);
    }

    [Fact]
    public void RecordRequest_MultipleRequests_ShouldAggregateCorrectly()
    {
        var testData = new[]
        {
            (platform: IndeedPlatform, success: true, latency: 100L),
            (platform: IndeedPlatform, success: true, latency: 150L),
            (platform: IndeedPlatform, success: false, latency: 50L),
            (platform: GlassdoorPlatform, success: true, latency: 200L),
            (platform: GlassdoorPlatform, success: false, latency: 100L),
        };

        foreach (var (platform, success, latency) in testData)
        {
            _monitoringService.RecordRequest(platform, success, latency);
        }

        var metrics = _monitoringService.GetCurrentMetrics();
        Assert.Equal(2, metrics.PerPlatformMetrics.Count);

        var indeedMetrics = metrics.PerPlatformMetrics[IndeedPlatform];
        Assert.Equal(3, indeedMetrics.TotalRequests);
        Assert.Equal(2, indeedMetrics.SuccessfulRequests);
        Assert.Equal(1, indeedMetrics.FailedRequests);

        var glassdoorMetrics = metrics.PerPlatformMetrics[GlassdoorPlatform];
        Assert.Equal(2, glassdoorMetrics.TotalRequests);
        Assert.Equal(1, glassdoorMetrics.SuccessfulRequests);
        Assert.Equal(1, glassdoorMetrics.FailedRequests);
    }

    #endregion

    #region Health Status Calculation Tests

    [Fact]
    public void GetPlatformHealth_HealthyStatus_SuccessRateGreaterThan90()
    {
        for (int i = 0; i < 19; i++)
        {
            _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);
        }
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100);

        var health = _monitoringService.GetPlatformHealth(IndeedPlatform);
        Assert.Equal("Healthy", health.Status.ToString());
        Assert.Equal(95, health.SuccessRate);
    }

    [Fact]
    public void GetPlatformHealth_DegradedStatus_SuccessRateBetween70And90()
    {
        for (int i = 0; i < 8; i++)
        {
            _monitoringService.RecordRequest(GlassdoorPlatform, success: true, 100);
        }
        for (int i = 0; i < 2; i++)
        {
            _monitoringService.RecordRequest(GlassdoorPlatform, success: false, 100);
        }

        var health = _monitoringService.GetPlatformHealth(GlassdoorPlatform);
        Assert.Equal("Degraded", health.Status.ToString());
        Assert.Equal(80, health.SuccessRate);
    }

    [Fact]
    public void GetPlatformHealth_UnhealthyStatus_SuccessRateLessThan70()
    {
        for (int i = 0; i < 3; i++)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: true, 100);
        }
        for (int i = 0; i < 7; i++)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: false, 100);
        }

        var health = _monitoringService.GetPlatformHealth(GooglePlatform);
        Assert.Equal("Unhealthy", health.Status.ToString());
        Assert.Equal(30, health.SuccessRate);
    }

    [Fact]
    public void GetPlatformHealth_ExactlyHealthyBoundary_SuccessRateEquals90()
    {
        for (int i = 0; i < 90; i++)
        {
            _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);
        }
        for (int i = 0; i < 10; i++)
        {
            _monitoringService.RecordRequest(IndeedPlatform, success: false, 100);
        }

        var health = _monitoringService.GetPlatformHealth(IndeedPlatform);
        Assert.Equal("Healthy", health.Status.ToString());
        Assert.Equal(90, health.SuccessRate);
    }

    [Fact]
    public void GetPlatformHealth_ExactlyDegradedBoundary_SuccessRateEquals70()
    {
        for (int i = 0; i < 70; i++)
        {
            _monitoringService.RecordRequest(GlassdoorPlatform, success: true, 100);
        }
        for (int i = 0; i < 30; i++)
        {
            _monitoringService.RecordRequest(GlassdoorPlatform, success: false, 100);
        }

        var health = _monitoringService.GetPlatformHealth(GlassdoorPlatform);
        Assert.Equal("Degraded", health.Status.ToString());
        Assert.Equal(70, health.SuccessRate);
    }

    [Fact]
    public void GetPlatformHealth_UnmonitoredPlatform_ShouldReturnHealthy()
    {
        var health = _monitoringService.GetPlatformHealth("UnknownPlatform");
        Assert.NotNull(health);
        Assert.Equal("UnknownPlatform", health.PlatformName);
        Assert.Equal("Healthy", health.Status.ToString());
        Assert.Equal(100, health.SuccessRate);
        Assert.Equal(0, health.ErrorCount);
    }

    #endregion

    #region Platform Health Tests

    [Fact]
    public void GetPlatformHealth_SinglePlatform_ShouldReturnCorrectMetrics()
    {
        for (int i = 0; i < 9; i++)
        {
            _monitoringService.RecordRequest(IndeedPlatform, success: true, 100 + i);
        }
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 150);

        var health = _monitoringService.GetPlatformHealth(IndeedPlatform);
        Assert.NotNull(health);
        Assert.Equal(IndeedPlatform, health.PlatformName);
        Assert.Equal(90, health.SuccessRate);
        Assert.Equal(1, health.ErrorCount);
        Assert.Equal("Healthy", health.Status.ToString());
    }

    [Fact]
    public void GetAllPlatformHealth_MultiplePlatforms_ShouldReturnAllHealthStatuses()
    {
        for (int i = 0; i < 19; i++)
        {
            _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);
        }
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100);

        for (int i = 0; i < 8; i++)
        {
            _monitoringService.RecordRequest(GlassdoorPlatform, success: true, 100);
        }
        for (int i = 0; i < 2; i++)
        {
            _monitoringService.RecordRequest(GlassdoorPlatform, success: false, 100);
        }

        for (int i = 0; i < 3; i++)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: true, 100);
        }
        for (int i = 0; i < 7; i++)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: false, 100);
        }

        var allHealth = _monitoringService.GetAllPlatformHealth();

        Assert.Equal(3, allHealth.Count);

        var indeedHealth = allHealth.FirstOrDefault(h => h.PlatformName == IndeedPlatform);
        Assert.NotNull(indeedHealth);
        Assert.Equal("Healthy", indeedHealth!.Status.ToString());

        var glassdoorHealth = allHealth.FirstOrDefault(h => h.PlatformName == GlassdoorPlatform);
        Assert.NotNull(glassdoorHealth);
        Assert.Equal("Degraded", glassdoorHealth!.Status.ToString());

        var googleHealth = allHealth.FirstOrDefault(h => h.PlatformName == GooglePlatform);
        Assert.NotNull(googleHealth);
        Assert.Equal("Unhealthy", googleHealth!.Status.ToString());
    }

    [Fact]
    public void GetPlatformHealth_ShouldIncludeAllMetrics()
    {
        _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 50);

        var health = _monitoringService.GetPlatformHealth(IndeedPlatform);
        Assert.NotNull(health.PlatformName);
        Assert.NotEqual(default, health.Status);
        Assert.True(health.SuccessRate >= 0 && health.SuccessRate <= 100);
        Assert.True(health.ErrorCount >= 0);
        Assert.NotEqual(default, health.LastChecked);
    }

    [Fact]
    public void GetPlatformHealth_ShouldUpdateLastCheckedTimestamp()
    {
        var beforeTime = DateTimeOffset.UtcNow;
        _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);

        var initialHealth = _monitoringService.GetPlatformHealth(IndeedPlatform);
        Assert.True(initialHealth.LastChecked >= beforeTime.AddSeconds(-1));

        Thread.Sleep(10);
        var firstCheck = initialHealth.LastChecked;

        var updatedHealth = _monitoringService.GetPlatformHealth(IndeedPlatform);
        Assert.True(updatedHealth.LastChecked >= firstCheck);
    }

    #endregion

    #region Metrics Retrieval Tests

    [Fact]
    public void GetCurrentMetrics_ShouldReturnSnapshotOfAllMetrics()
    {
        _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);
        _monitoringService.RecordRequest(GlassdoorPlatform, success: false, 200);

        var metrics = _monitoringService.GetCurrentMetrics();
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics.PerPlatformMetrics.Count);
        Assert.True(metrics.PerPlatformMetrics.ContainsKey(IndeedPlatform));
        Assert.True(metrics.PerPlatformMetrics.ContainsKey(GlassdoorPlatform));
    }

    [Fact]
    public void GetCurrentMetrics_ShouldIncludeAllPlatforms()
    {
        var platforms = new[] { IndeedPlatform, GlassdoorPlatform, GooglePlatform };
        foreach (var platform in platforms)
        {
            _monitoringService.RecordRequest(platform, success: true, 100);
        }

        var metrics = _monitoringService.GetCurrentMetrics();
        Assert.Equal(3, metrics.PerPlatformMetrics.Count);
        foreach (var platform in platforms)
        {
            Assert.True(metrics.PerPlatformMetrics.ContainsKey(platform));
        }
    }

    [Fact]
    public void GetCurrentMetrics_TimestampShouldBeCurrent()
    {
        var beforeTime = DateTimeOffset.UtcNow;
        _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);

        var metrics = _monitoringService.GetCurrentMetrics();
        var afterTime = DateTimeOffset.UtcNow;

        Assert.NotEqual(default, metrics.Timestamp);
        Assert.True(metrics.Timestamp >= beforeTime);
        Assert.True(metrics.Timestamp <= afterTime);
    }

    [Fact]
    public void GetCurrentMetrics_ShouldAggregateErrorCategoriesCorrectly()
    {
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100, "TimeoutError");
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100, "TimeoutError");
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100, "NetworkError");
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100, "ParseError");
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100, "ParseError");

        var metrics = _monitoringService.GetCurrentMetrics();
        var indeedMetrics = metrics.PerPlatformMetrics[IndeedPlatform];
        Assert.Equal(3, indeedMetrics.ErrorCategories.Count);
        Assert.Equal(2, indeedMetrics.ErrorCategories["TimeoutError"]);
        Assert.Equal(1, indeedMetrics.ErrorCategories["NetworkError"]);
        Assert.Equal(2, indeedMetrics.ErrorCategories["ParseError"]);
    }

    [Fact]
    public void GetCurrentMetrics_NoRequests_ShouldReturnEmptyMetrics()
    {
        var metrics = _monitoringService.GetCurrentMetrics();
        Assert.NotNull(metrics);
        Assert.Empty(metrics.PerPlatformMetrics);
        Assert.NotEqual(default, metrics.Timestamp);
    }

    #endregion

    #region Alert Threshold Tests

    [Fact]
    public void ShouldAlert_UnhealthyStatus_ShouldReturnTrue()
    {
        for (int i = 0; i < 3; i++)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: true, 100);
        }
        for (int i = 0; i < 7; i++)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: false, 100);
        }

        var shouldAlert = _monitoringService.ShouldAlert(GooglePlatform);
        Assert.True(shouldAlert);
    }

    [Fact]
    public void ShouldAlert_HealthyStatus_ShouldReturnFalse()
    {
        for (int i = 0; i < 19; i++)
        {
            _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);
        }
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100);

        var shouldAlert = _monitoringService.ShouldAlert(IndeedPlatform);
        Assert.False(shouldAlert);
    }

    [Fact]
    public void ShouldAlert_DegradedStatusExactly70_ShouldReturnFalse()
    {
        for (int i = 0; i < 70; i++)
        {
            _monitoringService.RecordRequest(GlassdoorPlatform, success: true, 100);
        }
        for (int i = 0; i < 30; i++)
        {
            _monitoringService.RecordRequest(GlassdoorPlatform, success: false, 100);
        }

        var shouldAlert = _monitoringService.ShouldAlert(GlassdoorPlatform);
        Assert.False(shouldAlert);
    }

    [Fact]
    public void ShouldAlert_BelowDegradedBoundary_ShouldReturnTrue()
    {
        for (int i = 0; i < 69; i++)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: true, 100);
        }
        for (int i = 0; i < 31; i++)
        {
            _monitoringService.RecordRequest(GooglePlatform, success: false, 100);
        }

        var shouldAlert = _monitoringService.ShouldAlert(GooglePlatform);
        Assert.True(shouldAlert);
    }

    [Fact]
    public void ShouldAlert_UnmonitoredPlatform_ShouldReturnFalse()
    {
        var shouldAlert = _monitoringService.ShouldAlert("UnknownPlatform");
        Assert.False(shouldAlert);
    }

    #endregion

    #region Health Status Method Tests

    [Fact]
    public void CheckHealthStatus_ShouldReturnHealthStatus()
    {
        for (int i = 0; i < 9; i++)
        {
            _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);
        }
        _monitoringService.RecordRequest(IndeedPlatform, success: false, 100);

        var status = _monitoringService.CheckHealthStatus(IndeedPlatform);
        Assert.Equal("Healthy", status.ToString());
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public void ComplexScenario_MultipleHealthLevels_ShouldTrackAllCorrectly()
    {
        var platforms = new Dictionary<string, (int successCount, int failCount)>
        {
            { IndeedPlatform, (95, 5) },
            { GlassdoorPlatform, (75, 25) },
            { GooglePlatform, (50, 50) }
        };

        foreach (var (platform, (successCount, failCount)) in platforms)
        {
            for (int i = 0; i < successCount; i++)
            {
                _monitoringService.RecordRequest(platform, success: true, 100 + i);
            }
            for (int i = 0; i < failCount; i++)
            {
                _monitoringService.RecordRequest(platform, success: false, 50 + i, "GenericError");
            }
        }

        var allHealth = _monitoringService.GetAllPlatformHealth();
        var metrics = _monitoringService.GetCurrentMetrics();

        Assert.Equal(3, allHealth.Count);

        var indeedHealth = allHealth.Single(h => h.PlatformName == IndeedPlatform);
        Assert.Equal("Healthy", indeedHealth.Status.ToString());
        Assert.Equal(95, indeedHealth.SuccessRate);
        Assert.False(_monitoringService.ShouldAlert(IndeedPlatform));

        var glassdoorHealth = allHealth.Single(h => h.PlatformName == GlassdoorPlatform);
        Assert.Equal("Degraded", glassdoorHealth.Status.ToString());
        Assert.Equal(75, glassdoorHealth.SuccessRate);
        Assert.False(_monitoringService.ShouldAlert(GlassdoorPlatform));

        var googleHealth = allHealth.Single(h => h.PlatformName == GooglePlatform);
        Assert.Equal("Unhealthy", googleHealth.Status.ToString());
        Assert.Equal(50, googleHealth.SuccessRate);
        Assert.True(_monitoringService.ShouldAlert(GooglePlatform));

        Assert.Equal(3, metrics.PerPlatformMetrics.Count);
        Assert.Equal(100, metrics.PerPlatformMetrics[IndeedPlatform].TotalRequests);
        Assert.Equal(100, metrics.PerPlatformMetrics[GlassdoorPlatform].TotalRequests);
        Assert.Equal(100, metrics.PerPlatformMetrics[GooglePlatform].TotalRequests);
    }

    [Fact]
    public void ComplexScenario_ManyErrorCategories_ShouldAggregateAllCategories()
    {
        var errorCategories = new[] { "TimeoutError", "NetworkError", "ParseError", "AuthError", "RateLimitError" };
        var errorCounts = new[] { 10, 8, 5, 3, 2 };

        for (int i = 0; i < errorCategories.Length; i++)
        {
            for (int j = 0; j < errorCounts[i]; j++)
            {
                _monitoringService.RecordRequest(IndeedPlatform, success: false, 100, errorCategories[i]);
            }
        }

        for (int i = 0; i < 100; i++)
        {
            _monitoringService.RecordRequest(IndeedPlatform, success: true, 100);
        }

        var metrics = _monitoringService.GetCurrentMetrics();
        var indeedMetrics = metrics.PerPlatformMetrics[IndeedPlatform];
        Assert.Equal(5, indeedMetrics.ErrorCategories.Count);
        Assert.Equal(10, indeedMetrics.ErrorCategories["TimeoutError"]);
        Assert.Equal(8, indeedMetrics.ErrorCategories["NetworkError"]);
        Assert.Equal(5, indeedMetrics.ErrorCategories["ParseError"]);
        Assert.Equal(3, indeedMetrics.ErrorCategories["AuthError"]);
        Assert.Equal(2, indeedMetrics.ErrorCategories["RateLimitError"]);
        Assert.Equal(100, indeedMetrics.SuccessfulRequests);
        Assert.Equal(28, indeedMetrics.FailedRequests);
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void RecordRequest_NullPlatformName_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _monitoringService.RecordRequest(null!, success: true, 100));
    }

    [Fact]
    public void RecordRequest_EmptyPlatformName_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _monitoringService.RecordRequest(string.Empty, success: true, 100));
    }

    [Fact]
    public void RecordRequest_WhitespacePlatformName_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _monitoringService.RecordRequest("   ", success: true, 100));
    }

    [Fact]
    public void GetPlatformHealth_NullPlatformName_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _monitoringService.GetPlatformHealth(null!));
    }

    #endregion
}
