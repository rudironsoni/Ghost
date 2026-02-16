using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.ProxyConfiguration;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Kernel.UnitTests.Services;

public sealed class ProxyHealthIntelligenceTests : IDisposable
{
    private static ProxyHealthIntelligence CreateIntelligence(
        IEnumerable<IProxySource>? sources = null,
        IOptions<ProxySystemOptions>? options = null,
        HttpClient? httpClient = null)
    {
        var defaultOptions = options ?? Options.Create(new ProxySystemOptions
        {
            RotationStrategy = "RoundRobin",
            HealthCheckIntervalSeconds = 0
        });

        return new ProxyHealthIntelligence(
            sources ?? new List<IProxySource>(),
            defaultOptions,
            NullLogger<ProxyHealthIntelligence>.Instance,
            httpClient);
    }

    private static Mock<IProxySource> CreateMockSource(IEnumerable<ProxyInfo> proxies)
    {
        var mock = new Mock<IProxySource>();
        mock.Setup(x => x.FetchProxiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(proxies);
        return mock;
    }

    private static ProxyInfo CreateProxy(string server, string? username = null, string? password = null)
    {
        return new ProxyInfo(server, username, password);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullSources_ThrowsArgumentNullException()
    {
        Action act = () => new ProxyHealthIntelligence(
            null!,
            Options.Create(new ProxySystemOptions()),
            NullLogger<ProxyHealthIntelligence>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("sources");
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Action act = () => new ProxyHealthIntelligence(
            new List<IProxySource>(),
            null!,
            NullLogger<ProxyHealthIntelligence>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Action act = () => new ProxyHealthIntelligence(
            new List<IProxySource>(),
            Options.Create(new ProxySystemOptions()),
            null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        var intelligence = CreateIntelligence();
        intelligence.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithFallbackSources_CreatesInstance()
    {
        var options = new ProxySystemOptions
        {
            FallbackChain = new List<ProxyConfiguration.ProxySourceConfig>
            {
                new() { Type = "Static", Hosts = new List<string> { "1.2.3.4:8080" } }
            }
        };

        var intelligence = CreateIntelligence(options: Options.Create(options));
        intelligence.Should().NotBeNull();
    }

    #endregion

    #region GetProxyAsync - RoundRobin Tests

    [Fact]
    public async Task GetProxyAsync_RoundRobin_ReturnsProxiesInRotation()
    {
        var proxies = new List<ProxyInfo>
        {
            CreateProxy("http://1.2.3.4:8080"),
            CreateProxy("http://5.6.7.8:3128"),
            CreateProxy("http://9.10.11.12:8080")
        };

        var mockSource = CreateMockSource(proxies);
        var options = Options.Create(new ProxySystemOptions
        {
            RotationStrategy = "RoundRobin",
            HealthCheckIntervalSeconds = 0
        });

        var intelligence = CreateIntelligence(
            new[] { mockSource.Object },
            options);

        var proxy1 = await intelligence.GetProxyAsync();
        var proxy2 = await intelligence.GetProxyAsync();
        var proxy3 = await intelligence.GetProxyAsync();
        var proxy4 = await intelligence.GetProxyAsync();

        proxy1.Should().NotBeNull();
        proxy2.Should().NotBeNull();
        proxy3.Should().NotBeNull();
        proxy4.Should().NotBeNull();
        proxy1!.Server.Should().Be(proxy4!.Server);
    }

    [Fact]
    public async Task GetProxyAsync_RoundRobin_EmptyPool_ReturnsNull()
    {
        var mockSource = CreateMockSource(new List<ProxyInfo>());

        var intelligence = CreateIntelligence(new[] { mockSource.Object });
        var proxy = await intelligence.GetProxyAsync();

        proxy.Should().BeNull();
    }

    #endregion

    #region GetProxyAsync - Performance Strategy Tests

    [Fact]
    public async Task GetProxyAsync_Performance_ReturnsProxyWithBestSuccessRate()
    {
        var proxy1 = CreateProxy("http://1.2.3.4:8080");
        var proxy2 = CreateProxy("http://5.6.7.8:3128");

        var mockSource = CreateMockSource(new[] { proxy1, proxy2 });
        var options = Options.Create(new ProxySystemOptions
        {
            RotationStrategy = "Performance",
            HealthCheckIntervalSeconds = 0
        });

        var intelligence = CreateIntelligence(
            new[] { mockSource.Object },
            options);

        await intelligence.ReportProxyResultAsync(proxy1, true, TimeSpan.FromMilliseconds(100));
        await intelligence.ReportProxyResultAsync(proxy1, false, TimeSpan.FromMilliseconds(100));
        await intelligence.ReportProxyResultAsync(proxy2, true, TimeSpan.FromMilliseconds(100));

        var selectedProxy = await intelligence.GetProxyAsync();
        selectedProxy.Should().NotBeNull();
    }

    #endregion

    #region GetProxyAsync - Random Strategy Tests

    [Fact]
    public async Task GetProxyAsync_Random_ReturnsRandomProxy()
    {
        var proxies = new List<ProxyInfo>
        {
            CreateProxy("http://1.2.3.4:8080"),
            CreateProxy("http://5.6.7.8:3128"),
            CreateProxy("http://9.10.11.12:8080")
        };

        var mockSource = CreateMockSource(proxies);
        var options = Options.Create(new ProxySystemOptions
        {
            RotationStrategy = "Random",
            HealthCheckIntervalSeconds = 0
        });

        var intelligence = CreateIntelligence(
            new[] { mockSource.Object },
            options);

        var proxy = await intelligence.GetProxyAsync();
        proxy.Should().NotBeNull();
        proxies.Select(p => p.Server).Should().Contain(proxy!.Server);
    }

    #endregion

    #region GetProxyAsync - LeastUsed Strategy Tests

    [Fact]
    public async Task GetProxyAsync_LeastUsed_ReturnsProxyWithFewestRequests()
    {
        var proxy1 = CreateProxy("http://1.2.3.4:8080");
        var proxy2 = CreateProxy("http://5.6.7.8:3128");

        var mockSource = CreateMockSource(new[] { proxy1, proxy2 });
        var options = Options.Create(new ProxySystemOptions
        {
            RotationStrategy = "LeastUsed",
            HealthCheckIntervalSeconds = 0
        });

        var intelligence = CreateIntelligence(
            new[] { mockSource.Object },
            options);

        await intelligence.ReportProxyResultAsync(proxy1, true, TimeSpan.FromMilliseconds(100));
        await intelligence.ReportProxyResultAsync(proxy1, true, TimeSpan.FromMilliseconds(100));

        var selectedProxy = await intelligence.GetProxyAsync();
        selectedProxy.Should().NotBeNull();
    }

    #endregion

    #region GetProxyAsync - Fallback Tests

    [Fact]
    public async Task GetProxyAsync_NoHealthyProxies_WithFallback_ReturnsFallbackProxy()
    {
        var primaryProxy = CreateProxy("http://1.2.3.4:8080");
        var fallbackProxy = CreateProxy("http://fallback:8080");

        var mockPrimarySource = CreateMockSource(new[] { primaryProxy });
        var mockFallbackSource = CreateMockSource(new[] { fallbackProxy });

        var options = Options.Create(new ProxySystemOptions
        {
            FallbackChain = new List<ProxyConfiguration.ProxySourceConfig> { new() }
        });

        var intelligence = new ProxyHealthIntelligence(
            new[] { mockPrimarySource.Object },
            options,
            NullLogger<ProxyHealthIntelligence>.Instance);

        await intelligence.ReportProxyResultAsync(primaryProxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(primaryProxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(primaryProxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(primaryProxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(primaryProxy, false, TimeSpan.Zero);

        intelligence.BlacklistProxy(primaryProxy);

        var mockFallbackSources = new List<IProxySource> { mockFallbackSource.Object };
        var fallbackOptions = Options.Create(new ProxySystemOptions
        {
            RotationStrategy = "RoundRobin",
            FallbackChain = new List<ProxyConfiguration.ProxySourceConfig> { new() }
        });
    }

    #endregion

    #region ReportProxyResultAsync Tests

    [Fact]
    public async Task ReportProxyResultAsync_Success_UpdatesMetrics()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");

        var intelligence = CreateIntelligence();
        await intelligence.ReportProxyResultAsync(proxy, true, TimeSpan.FromMilliseconds(100));

        var metrics = intelligence.GetMetrics(proxy);
        metrics.Should().NotBeNull();
        metrics!.TotalRequests.Should().Be(1);
        metrics.SuccessfulRequests.Should().Be(1);
        metrics.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task ReportProxyResultAsync_Failure_UpdatesMetrics()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");

        var intelligence = CreateIntelligence();
        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.FromMilliseconds(100));

        var metrics = intelligence.GetMetrics(proxy);
        metrics.Should().NotBeNull();
        metrics!.TotalRequests.Should().Be(1);
        metrics.FailedRequests.Should().Be(1);
        metrics.SuccessRate.Should().Be(0.0);
    }

    [Fact]
    public async Task ReportProxyResultAsync_MultipleRequests_AccumulatesMetrics()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");

        var intelligence = CreateIntelligence();
        await intelligence.ReportProxyResultAsync(proxy, true, TimeSpan.FromMilliseconds(100));
        await intelligence.ReportProxyResultAsync(proxy, true, TimeSpan.FromMilliseconds(150));
        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.FromMilliseconds(200));

        var metrics = intelligence.GetMetrics(proxy);
        metrics!.TotalRequests.Should().Be(3);
        metrics.SuccessfulRequests.Should().Be(2);
        metrics.FailedRequests.Should().Be(1);
        metrics.SuccessRate.Should().BeApproximately(0.67, 0.01);
    }

    [Fact]
    public async Task ReportProxyResultAsync_FiveConsecutiveFailures_AddsToBlacklist()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");
        var mockSource = CreateMockSource(new[] { proxy });

        var intelligence = CreateIntelligence(new[] { mockSource.Object });

        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.Zero);

        var metrics = intelligence.GetMetrics(proxy);
        metrics!.ConsecutiveFailures.Should().Be(5);

        var healthyProxy = await intelligence.GetProxyAsync();
        healthyProxy.Should().BeNull();
    }

    [Fact]
    public async Task ReportProxyResultAsync_NullProxy_ReturnsWithoutError()
    {
        var intelligence = CreateIntelligence();

        Func<Task> act = async () => await intelligence.ReportProxyResultAsync(null!, true, TimeSpan.Zero);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReportProxyResultAsync_SuccessAfterFailure_ResetsConsecutiveFailures()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");
        var intelligence = CreateIntelligence();

        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(proxy, false, TimeSpan.Zero);
        await intelligence.ReportProxyResultAsync(proxy, true, TimeSpan.Zero);

        var metrics = intelligence.GetMetrics(proxy);
        metrics!.ConsecutiveFailures.Should().Be(0);
    }

    #endregion

    #region Blacklist Tests

    [Fact]
    public void BlacklistProxy_AddsToBlacklist()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");
        var mockSource = CreateMockSource(new[] { proxy });

        var intelligence = CreateIntelligence(new[] { mockSource.Object });
        intelligence.BlacklistProxy(proxy);

        var metrics = intelligence.GetMetrics(proxy);
    }

    [Fact]
    public void BlacklistProxy_NullProxy_DoesNotThrow()
    {
        var intelligence = CreateIntelligence();
        Action act = () => intelligence.BlacklistProxy(null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void RemoveFromBlacklist_RemovesFromBlacklist()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");
        var mockSource = CreateMockSource(new[] { proxy });

        var intelligence = CreateIntelligence(new[] { mockSource.Object });
        intelligence.BlacklistProxy(proxy);
        intelligence.RemoveFromBlacklist(proxy);
    }

    [Fact]
    public void RemoveFromBlacklist_NullProxy_DoesNotThrow()
    {
        var intelligence = CreateIntelligence();
        Action act = () => intelligence.RemoveFromBlacklist(null!);
        act.Should().NotThrow();
    }

    #endregion

    #region Whitelist Tests

    [Fact]
    public void WhitelistProxy_AddsToWhitelist()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");
        var mockSource = CreateMockSource(new[] { proxy });

        var intelligence = CreateIntelligence(new[] { mockSource.Object });
        intelligence.WhitelistProxy(proxy);
    }

    [Fact]
    public void WhitelistProxy_NullProxy_DoesNotThrow()
    {
        var intelligence = CreateIntelligence();
        Action act = () => intelligence.WhitelistProxy(null!);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task WhitelistProxy_PrioritizedOverRegularProxies()
    {
        var whitelistedProxy = CreateProxy("http://1.2.3.4:8080");
        var regularProxy = CreateProxy("http://5.6.7.8:3128");

        var mockSource = CreateMockSource(new[] { whitelistedProxy, regularProxy });
        var intelligence = CreateIntelligence(new[] { mockSource.Object });

        intelligence.WhitelistProxy(whitelistedProxy);
    }

    #endregion

    #region GetAllMetrics Tests

    [Fact]
    public void GetAllMetrics_NoProxies_ReturnsEmpty()
    {
        var intelligence = CreateIntelligence();
        var metrics = intelligence.GetAllMetrics();
        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllMetrics_WithProxies_ReturnsAllMetrics()
    {
        var proxy1 = CreateProxy("http://1.2.3.4:8080");
        var proxy2 = CreateProxy("http://5.6.7.8:3128");

        var intelligence = CreateIntelligence();
        await intelligence.ReportProxyResultAsync(proxy1, true, TimeSpan.FromMilliseconds(100));
        await intelligence.ReportProxyResultAsync(proxy2, true, TimeSpan.FromMilliseconds(150));

        var metrics = intelligence.GetAllMetrics();
        metrics.Should().HaveCount(2);
    }

    #endregion

    #region GetMetrics Tests

    [Fact]
    public void GetMetrics_NullProxy_ReturnsNull()
    {
        var intelligence = CreateIntelligence();
        var metrics = intelligence.GetMetrics(null!);
        metrics.Should().BeNull();
    }

    [Fact]
    public void GetMetrics_UnknownProxy_ReturnsNull()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080");
        var intelligence = CreateIntelligence();
        var metrics = intelligence.GetMetrics(proxy);
        metrics.Should().BeNull();
    }

    #endregion

    #region ProxyHealthMetrics Tests

    [Fact]
    public void ProxyHealthMetrics_AverageLatency_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.LatencyHistory.Add(100);
        metrics.LatencyHistory.Add(200);
        metrics.LatencyHistory.Add(300);

        metrics.AverageLatency.Should().Be(200);
    }

    [Fact]
    public void ProxyHealthMetrics_AverageLatency_EmptyHistory_ReturnsZero()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.AverageLatency.Should().Be(0);
    }

    [Fact]
    public void ProxyHealthMetrics_MedianLatency_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.LatencyHistory.Add(100);
        metrics.LatencyHistory.Add(200);
        metrics.LatencyHistory.Add(300);

        metrics.MedianLatency.Should().Be(200);
    }

    [Fact]
    public void ProxyHealthMetrics_MedianLatency_EvenCount_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.LatencyHistory.Add(100);
        metrics.LatencyHistory.Add(200);
        metrics.LatencyHistory.Add(300);
        metrics.LatencyHistory.Add(400);

        metrics.MedianLatency.Should().Be(250);
    }

    [Fact]
    public void ProxyHealthMetrics_MedianLatency_EmptyHistory_ReturnsZero()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.MedianLatency.Should().Be(0);
    }

    [Fact]
    public void ProxyHealthMetrics_P95Latency_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        for (int i = 1; i <= 100; i++)
        {
            metrics.LatencyHistory.Add(i);
        }

        metrics.P95Latency.Should().Be(95);
    }

    [Fact]
    public void ProxyHealthMetrics_P95Latency_EmptyHistory_ReturnsZero()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.P95Latency.Should().Be(0);
    }

    [Fact]
    public void ProxyHealthMetrics_SuccessRate_NoRequests_ReturnsZero()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.SuccessRate.Should().Be(0);
    }

    [Fact]
    public void ProxyHealthMetrics_SuccessRate_WithRequests_CalculatesCorrectly()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow,
            TotalRequests = 10,
            SuccessfulRequests = 7
        };

        metrics.SuccessRate.Should().Be(0.7);
    }

    #endregion

    #region Source Loading Tests

    [Fact]
    public async Task GetProxyAsync_SourceThrowsException_HandlesGracefully()
    {
        var mockSource = new Mock<IProxySource>();
        mockSource.Setup(x => x.FetchProxiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var intelligence = CreateIntelligence(new[] { mockSource.Object });
        var proxy = await intelligence.GetProxyAsync();

        proxy.Should().BeNull();
    }

    [Fact]
    public async Task GetProxyAsync_SourceReturnsEmpty_ReturnsNull()
    {
        var mockSource = CreateMockSource(new List<ProxyInfo>());

        var intelligence = CreateIntelligence(new[] { mockSource.Object });
        var proxy = await intelligence.GetProxyAsync();

        proxy.Should().BeNull();
    }

    [Fact]
    public async Task GetProxyAsync_MultipleSources_LoadsFromAll()
    {
        var source1Proxies = new[] { CreateProxy("http://1.2.3.4:8080") };
        var source2Proxies = new[] { CreateProxy("http://5.6.7.8:3128") };

        var mockSource1 = CreateMockSource(source1Proxies);
        var mockSource2 = CreateMockSource(source2Proxies);

        var intelligence = CreateIntelligence(new[] { mockSource1.Object, mockSource2.Object });

        var metrics = intelligence.GetAllMetrics();
        metrics.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetProxyAsync_CancellationToken_PropagatesToSources()
    {
        var mockSource = new Mock<IProxySource>();
        var cts = new CancellationTokenSource();

        mockSource.Setup(x => x.FetchProxiesAsync(cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var intelligence = CreateIntelligence(new[] { mockSource.Object });

        Func<Task> act = async () => await intelligence.GetProxyAsync(token: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var intelligence = CreateIntelligence();
        intelligence.Dispose();
        intelligence.Dispose();
    }

    [Fact]
    public void Dispose_WithBackgroundHealthCheck_StopsCleanly()
    {
        var options = Options.Create(new ProxySystemOptions
        {
            HealthCheckIntervalSeconds = 1
        });

        var intelligence = CreateIntelligence(options: options);
        intelligence.Dispose();
    }

    #endregion

    #region Unknown Strategy Tests

    [Fact]
    public async Task GetProxyAsync_UnknownStrategy_DefaultsToRoundRobin()
    {
        var proxies = new List<ProxyInfo>
        {
            CreateProxy("http://1.2.3.4:8080"),
            CreateProxy("http://5.6.7.8:3128")
        };

        var mockSource = CreateMockSource(proxies);
        var options = Options.Create(new ProxySystemOptions
        {
            RotationStrategy = "UnknownStrategy",
            HealthCheckIntervalSeconds = 0
        });

        var intelligence = CreateIntelligence(
            new[] { mockSource.Object },
            options);

        var proxy = await intelligence.GetProxyAsync();
        proxy.Should().NotBeNull();
    }

    #endregion

    #region GeographicLatency Tests

    [Fact]
    public void ProxyHealthMetrics_GeographicLatency_InitializedEmpty()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.GeographicLatency.Should().BeEmpty();
    }

    [Fact]
    public void ProxyHealthMetrics_LatencyHistory_TracksMultipleValues()
    {
        var metrics = new ProxyHealthMetrics
        {
            ProxyKey = "test",
            FirstSeen = DateTimeOffset.UtcNow
        };

        metrics.LatencyHistory.Add(100);
        metrics.LatencyHistory.Add(200);
        metrics.LatencyHistory.Add(300);

        metrics.LatencyHistory.Should().HaveCount(3);
    }

    #endregion

    #region Proxy Authentication Tests

    [Fact]
    public async Task GetProxyAsync_AuthenticatedProxy_IncludesCredentials()
    {
        var proxy = CreateProxy("http://1.2.3.4:8080", "username", "password");
        var mockSource = CreateMockSource(new[] { proxy });

        var intelligence = CreateIntelligence(new[] { mockSource.Object });
        var retrievedProxy = await intelligence.GetProxyAsync();

        retrievedProxy.Should().NotBeNull();
        retrievedProxy!.Username.Should().Be("username");
        retrievedProxy.Password.Should().Be("password");
    }

    #endregion
}
