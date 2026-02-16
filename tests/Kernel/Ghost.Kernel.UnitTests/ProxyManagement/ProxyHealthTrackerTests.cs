using System.Net;
using Ghost.ProxyConfiguration;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Kernel.UnitTests.ProxyManagement;

public class ProxyHealthTrackerTests
{
    private readonly ProxyHealthTracker _tracker;

    public ProxyHealthTrackerTests()
    {
        _tracker = new ProxyHealthTracker(NullLogger<ProxyHealthTracker>.Instance);
    }

    [Fact]
    public void Constructor_InitializesEmptyMetrics()
    {
        IReadOnlyDictionary<string, ProxyHealthMetrics> metrics = _tracker.GetAllMetrics();
        Assert.Empty(metrics);
    }

    [Fact]
    public async Task RecordResultAsync_WithNullProxy_ReturnsCompletedTask()
    {
        Task result = _tracker.RecordResultAsync(null!, true, TimeSpan.FromMilliseconds(100));
        await result;
        Assert.Equal(Task.CompletedTask, result);
    }

    [Fact]
    public async Task RecordResultAsync_Success_IncrementsSuccessMetrics()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(100), HttpStatusCode.OK);

        ProxyHealthMetrics? metrics = _tracker.GetMetrics(proxy);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.TotalRequests);
        Assert.Equal(1, metrics.SuccessfulRequests);
        Assert.Equal(0, metrics.FailedRequests);
        Assert.Equal(0, metrics.ConsecutiveFailures);
    }

    [Fact]
    public async Task RecordResultAsync_Failure_IncrementsFailureMetrics()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        await _tracker.RecordResultAsync(proxy, false, TimeSpan.FromMilliseconds(100), HttpStatusCode.BadGateway);

        ProxyHealthMetrics? metrics = _tracker.GetMetrics(proxy);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.TotalRequests);
        Assert.Equal(0, metrics.SuccessfulRequests);
        Assert.Equal(1, metrics.FailedRequests);
        Assert.Equal(1, metrics.ConsecutiveFailures);
    }

    [Fact]
    public async Task RecordResultAsync_MultipleCalls_AccumulatesMetrics()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(100));
        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(150));
        await _tracker.RecordResultAsync(proxy, false, TimeSpan.FromMilliseconds(200));

        ProxyHealthMetrics? metrics = _tracker.GetMetrics(proxy);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics.TotalRequests);
        Assert.Equal(2, metrics.SuccessfulRequests);
        Assert.Equal(1, metrics.FailedRequests);
    }

    [Fact]
    public async Task RecordResultAsync_ConsecutiveFailures_TracksCorrectly()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        // 3 successes
        for (int i = 0; i < 3; i++)
        {
            await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(100));
        }

        // 2 failures
        await _tracker.RecordResultAsync(proxy, false, TimeSpan.FromMilliseconds(100));
        await _tracker.RecordResultAsync(proxy, false, TimeSpan.FromMilliseconds(100));

        // 1 success resets consecutive failures
        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(100));

        ProxyHealthMetrics? metrics = _tracker.GetMetrics(proxy);
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.ConsecutiveFailures);
    }

    [Fact]
    public void GetMetrics_WithNullProxy_ReturnsNull()
    {
        ProxyHealthMetrics? metrics = _tracker.GetMetrics(null!);
        Assert.Null(metrics);
    }

    [Fact]
    public void GetMetrics_UntrackedProxy_ReturnsNull()
    {
        ProxyInfo proxy = CreateTestProxy("http://untracked:8080");
        ProxyHealthMetrics? metrics = _tracker.GetMetrics(proxy);
        Assert.Null(metrics);
    }

    [Fact]
    public void GetOrCreateMetrics_CreatesNewMetricsForNewProxy()
    {
        ProxyInfo proxy = CreateTestProxy("http://new:8080");
        ProxyHealthMetrics metrics = _tracker.GetOrCreateMetrics(proxy);

        Assert.NotNull(metrics);
        Assert.Equal("http://new:8080|", metrics.ProxyKey);
    }

    [Fact]
    public void GetOrCreateMetrics_ReturnsExistingMetricsForTrackedProxy()
    {
        ProxyInfo proxy = CreateTestProxy("http://existing:8080");
        _tracker.GetOrCreateMetrics(proxy);

        ProxyHealthMetrics metrics2 = _tracker.GetOrCreateMetrics(proxy);

        Assert.NotNull(metrics2);
        Assert.Equal(0, metrics2.TotalRequests);
    }

    [Fact]
    public void IsProxyUnhealthy_WithNoFailures_ReturnsFalse()
    {
        ProxyInfo proxy = CreateTestProxy("http://healthy:8080");
        Assert.False(_tracker.IsProxyUnhealthy(proxy));
    }

    [Fact]
    public async Task IsProxyUnhealthy_With5ConsecutiveFailures_ReturnsTrue()
    {
        ProxyInfo proxy = CreateTestProxy("http://unhealthy:8080");

        for (int i = 0; i < 5; i++)
        {
            await _tracker.RecordResultAsync(proxy, false, TimeSpan.FromMilliseconds(100));
        }

        Assert.True(_tracker.IsProxyUnhealthy(proxy));
    }

    [Fact]
    public void GetSuccessRate_WithNoRequests_Returns1()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        _tracker.GetOrCreateMetrics(proxy);

        double rate = _tracker.GetSuccessRate(proxy);
        Assert.Equal(0, rate);
    }

    [Fact]
    public async Task GetSuccessRate_WithMixedResults_ReturnsCorrectRate()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(100));
        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(100));
        await _tracker.RecordResultAsync(proxy, false, TimeSpan.FromMilliseconds(100));

        double rate = _tracker.GetSuccessRate(proxy);
        Assert.Equal(2.0 / 3.0, rate, precision: 5);
    }

    [Fact]
    public void GetAverageLatency_WithNoLatency_Returns0()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        _tracker.GetOrCreateMetrics(proxy);

        double latency = _tracker.GetAverageLatency(proxy);
        Assert.Equal(0, latency);
    }

    [Fact]
    public async Task GetAverageLatency_WithLatencyRecords_ReturnsAverage()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(100));
        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(200));
        await _tracker.RecordResultAsync(proxy, true, TimeSpan.FromMilliseconds(300));

        double latency = _tracker.GetAverageLatency(proxy);
        Assert.Equal(200, latency);
    }

    [Fact]
    public void ResetMetrics_ClearsExistingMetrics()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        _tracker.GetOrCreateMetrics(proxy);

        _tracker.ResetMetrics(proxy);

        ProxyHealthMetrics? metrics = _tracker.GetMetrics(proxy);
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.TotalRequests);
    }

    [Fact]
    public void GetTrackedProxyKeys_ReturnsAllTrackedKeys()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        _tracker.GetOrCreateMetrics(proxy1);
        _tracker.GetOrCreateMetrics(proxy2);

        IEnumerable<string> keys = _tracker.GetTrackedProxyKeys();
        Assert.Equal(2, keys.Count());
    }

    [Fact]
    public void IsTracked_KnownProxy_ReturnsTrue()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        _tracker.GetOrCreateMetrics(proxy);

        Assert.True(_tracker.IsTracked(proxy));
    }

    [Fact]
    public void IsTracked_UnknownProxy_ReturnsFalse()
    {
        ProxyInfo proxy = CreateTestProxy("http://unknown:8080");
        Assert.False(_tracker.IsTracked(proxy));
    }

    [Fact]
    public void ProxyHealthMetrics_SuccessRate_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics
        {
            TotalRequests = 10,
            SuccessfulRequests = 7,
            FailedRequests = 3
        };

        Assert.Equal(0.7, metrics.SuccessRate);
    }

    [Fact]
    public void ProxyHealthMetrics_AverageLatency_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics();
        metrics.LatencyHistory.Add(100);
        metrics.LatencyHistory.Add(200);
        metrics.LatencyHistory.Add(300);

        Assert.Equal(200, metrics.AverageLatency);
    }

    [Fact]
    public void ProxyHealthMetrics_MedianLatency_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics();
        metrics.LatencyHistory.Add(100);
        metrics.LatencyHistory.Add(200);
        metrics.LatencyHistory.Add(300);
        metrics.LatencyHistory.Add(400);
        metrics.LatencyHistory.Add(500);

        Assert.Equal(300, metrics.MedianLatency);
    }

    [Fact]
    public void ProxyHealthMetrics_P95Latency_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics();
        for (int i = 1; i <= 100; i++)
        {
            metrics.LatencyHistory.Add(i);
        }

        Assert.Equal(95, metrics.P95Latency);
    }

    private static ProxyInfo CreateTestProxy(string server)
    {
        return new ProxyInfo
        {
            Server = server,
            Username = null,
            Password = null
        };
    }
}
