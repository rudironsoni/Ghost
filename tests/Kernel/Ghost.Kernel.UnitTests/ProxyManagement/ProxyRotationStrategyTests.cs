using Ghost.ProxyConfiguration;
using Ghost.ProxyManagement;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.UnitTests.ProxyManagement;

public sealed class ProxyRotationStrategyTests : ReliabilityTestBase
{
    private readonly ProxyHealthTracker _healthTracker;
    private readonly ProxyRotationStrategy _strategy;

    public ProxyRotationStrategyTests(ITestOutputHelper output) : base(output)
    {
        _healthTracker = new ProxyHealthTracker(NullLogger<ProxyHealthTracker>.Instance);
        _strategy = new ProxyRotationStrategy(_healthTracker, NullLogger<ProxyRotationStrategy>.Instance);
    }

    [Fact]
    public void Constructor_WithNullHealthTracker_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ProxyRotationStrategy(null!, NullLogger<ProxyRotationStrategy>.Instance));
    }

    [Fact]
    public void SelectProxy_EmptyList_ReturnsNull()
    {
        List<ProxyInfo> emptyList = [];
        ProxyInfo? result = _strategy.SelectProxy(emptyList, RotationStrategyType.RoundRobin);
        Assert.Null(result);
    }

    [Fact]
    public void SelectProxy_SingleProxy_ReturnsThatProxy()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        List<ProxyInfo> proxies = [proxy];

        ProxyInfo? result = _strategy.SelectProxy(proxies, RotationStrategyType.RoundRobin);

        Assert.Equal(proxy, result);
    }

    [Fact]
    public void SelectRoundRobin_MultipleProxies_CyclesThrough()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");
        ProxyInfo proxy3 = CreateTestProxy("http://test3:8080");
        List<ProxyInfo> proxies = [proxy1, proxy2, proxy3];

        ProxyInfo? result1 = _strategy.SelectRoundRobin(proxies);
        ProxyInfo? result2 = _strategy.SelectRoundRobin(proxies);
        ProxyInfo? result3 = _strategy.SelectRoundRobin(proxies);
        ProxyInfo? result4 = _strategy.SelectRoundRobin(proxies); // Should cycle back

        Assert.Equal(proxy2, result1); // Interlocked.Increment starts at 1
        Assert.Equal(proxy3, result2);
        Assert.Equal(proxy1, result3);
        Assert.Equal(proxy2, result4);
    }

    [Fact]
    public async Task SelectByPerformance_ReturnsBestPerformingProxyAsync()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");
        ProxyInfo proxy3 = CreateTestProxy("http://test3:8080");
        List<ProxyInfo> proxies = [proxy1, proxy2, proxy3];

        // Setup: proxy2 has best success rate
        await _healthTracker.RecordResultAsync(proxy1, true, TimeSpan.FromMilliseconds(100));
        await _healthTracker.RecordResultAsync(proxy1, false, TimeSpan.FromMilliseconds(100));
        // proxy1: 50% success

        await _healthTracker.RecordResultAsync(proxy2, true, TimeSpan.FromMilliseconds(100));
        await _healthTracker.RecordResultAsync(proxy2, true, TimeSpan.FromMilliseconds(100));
        // proxy2: 100% success

        await _healthTracker.RecordResultAsync(proxy3, false, TimeSpan.FromMilliseconds(100));
        await _healthTracker.RecordResultAsync(proxy3, false, TimeSpan.FromMilliseconds(100));
        // proxy3: 0% success

        ProxyInfo? result = _strategy.SelectByPerformance(proxies);

        Assert.Equal(proxy2, result);
    }

    [Fact]
    public void SelectRandom_ReturnsProxyFromList()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");
        ProxyInfo proxy3 = CreateTestProxy("http://test3:8080");
        List<ProxyInfo> proxies = [proxy1, proxy2, proxy3];

        ProxyInfo? result = ProxyRotationStrategy.SelectRandom(proxies);

        Assert.Contains(result, proxies);
    }

    [Fact]
    public async Task SelectLeastUsed_ReturnsProxyWithFewestRequestsAsync()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");
        ProxyInfo proxy3 = CreateTestProxy("http://test3:8080");
        List<ProxyInfo> proxies = [proxy1, proxy2, proxy3];

        // Setup: proxy2 has fewest requests
        await _healthTracker.RecordResultAsync(proxy1, true, TimeSpan.FromMilliseconds(100));
        await _healthTracker.RecordResultAsync(proxy1, true, TimeSpan.FromMilliseconds(100));
        // proxy1: 2 requests

        // proxy2: 0 requests

        await _healthTracker.RecordResultAsync(proxy3, true, TimeSpan.FromMilliseconds(100));
        // proxy3: 1 request

        ProxyInfo? result = _strategy.SelectLeastUsed(proxies);

        Assert.Equal(proxy2, result);
    }

    [Fact]
    public void GetHealthyProxies_AllHealthy_ReturnsAll()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        var proxyPool = new Dictionary<string, ProxyInfo>
        {
            ["http://test1:8080|"] = proxy1,
            ["http://test2:8080|"] = proxy2
        };

        var blacklistManager = new ProxyBlacklistManager();

        List<ProxyInfo> healthy = _strategy.GetHealthyProxies(proxyPool, blacklistManager);

        Assert.Equal(2, healthy.Count);
    }

    [Fact]
    public void GetHealthyProxies_ExcludesBlacklisted()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        var proxyPool = new Dictionary<string, ProxyInfo>
        {
            ["http://test1:8080|"] = proxy1,
            ["http://test2:8080|"] = proxy2
        };

        var blacklistManager = new ProxyBlacklistManager();
        blacklistManager.Blacklist(proxy1);

        List<ProxyInfo> healthy = _strategy.GetHealthyProxies(proxyPool, blacklistManager);

        Assert.Single(healthy);
        Assert.Contains(proxy2, healthy);
    }

    [Fact]
    public void GetHealthyProxies_IncludesWhitelistedFirst()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        var proxyPool = new Dictionary<string, ProxyInfo>
        {
            ["http://test1:8080|"] = proxy1,
            ["http://test2:8080|"] = proxy2
        };

        var blacklistManager = new ProxyBlacklistManager();
        blacklistManager.Whitelist(proxy2);

        List<ProxyInfo> healthy = _strategy.GetHealthyProxies(proxyPool, blacklistManager);

        Assert.Equal(2, healthy.Count);
        Assert.Equal(proxy2, healthy[0]); // Whitelisted first
    }

    [Fact]
    public async Task GetHealthyProxies_ExcludesUnhealthyAsync()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        var proxyPool = new Dictionary<string, ProxyInfo>
        {
            ["http://test1:8080|"] = proxy1,
            ["http://test2:8080|"] = proxy2
        };

        var blacklistManager = new ProxyBlacklistManager();

        // Make proxy1 unhealthy (below 50% success rate)
        await _healthTracker.RecordResultAsync(proxy1, false, TimeSpan.FromMilliseconds(100));
        await _healthTracker.RecordResultAsync(proxy1, false, TimeSpan.FromMilliseconds(100));
        await _healthTracker.RecordResultAsync(proxy1, true, TimeSpan.FromMilliseconds(100));
        // 33% success rate

        List<ProxyInfo> healthy = _strategy.GetHealthyProxies(proxyPool, blacklistManager);

        Assert.Single(healthy);
        Assert.Contains(proxy2, healthy);
    }

    [Fact]
    public void ParseStrategy_NullOrEmpty_ReturnsRoundRobin()
    {
        Assert.Equal(RotationStrategyType.RoundRobin, ProxyRotationStrategy.ParseStrategy(null));
        Assert.Equal(RotationStrategyType.RoundRobin, ProxyRotationStrategy.ParseStrategy(""));
    }

    [Theory]
    [InlineData("roundrobin", RotationStrategyType.RoundRobin)]
    [InlineData("RoundRobin", RotationStrategyType.RoundRobin)]
    [InlineData("performance", RotationStrategyType.Performance)]
    [InlineData("random", RotationStrategyType.Random)]
    [InlineData("leastused", RotationStrategyType.LeastUsed)]
    public void ParseStrategy_ValidStrategies_ReturnsCorrectType(string input, RotationStrategyType expected)
    {
        Assert.Equal(expected, ProxyRotationStrategy.ParseStrategy(input));
    }

    [Fact]
    public void ParseStrategy_UnknownStrategy_ReturnsRoundRobin()
    {
        Assert.Equal(RotationStrategyType.RoundRobin, ProxyRotationStrategy.ParseStrategy("unknown"));
    }

    [Fact]
    public void SelectProxy_DefaultStrategy_IsRoundRobin()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");
        List<ProxyInfo> proxies = [proxy1, proxy2];

        ProxyInfo? result1 = _strategy.SelectProxy(proxies);
        ProxyInfo? result2 = _strategy.SelectProxy(proxies);

        Assert.Equal(proxy2, result1); // Interlocked.Increment starts at 1
        Assert.Equal(proxy1, result2);
    }

    private static ProxyInfo CreateTestProxy(string server)
    {
        return new ProxyInfo(server, null, null);
    }
}
