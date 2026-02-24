using Ghost.ProxyConfiguration;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Kernel.UnitTests.ProxyManagement;

public class ProxyBlacklistManagerTests : ReliabilityTestBase
{
    private readonly ProxyBlacklistManager _manager;

    public ProxyBlacklistManagerTests(ITestOutputHelper output) : base(output)
    {
        _manager = new ProxyBlacklistManager(NullLogger<ProxyBlacklistManager>.Instance);
    }

    [Fact]
    public void Blacklist_WithNullProxy_DoesNothing()
    {
        _manager.Blacklist(null!);
        Assert.Equal(0, _manager.BlacklistCount);
    }

    [Fact]
    public void Blacklist_ValidProxy_AddsToBlacklist()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        _manager.Blacklist(proxy);

        Assert.Equal(1, _manager.BlacklistCount);
        Assert.True(_manager.IsBlacklisted(proxy));
    }

    [Fact]
    public void Blacklist_SameProxyTwice_DoesNotDuplicate()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        _manager.Blacklist(proxy);
        _manager.Blacklist(proxy);

        Assert.Equal(1, _manager.BlacklistCount);
    }

    [Fact]
    public void RemoveFromBlacklist_WithNullProxy_DoesNothing()
    {
        _manager.RemoveFromBlacklist(null!);
        Assert.Equal(0, _manager.BlacklistCount);
    }

    [Fact]
    public void RemoveFromBlacklist_BlacklistedProxy_RemovesFromBlacklist()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        _manager.Blacklist(proxy);

        _manager.RemoveFromBlacklist(proxy);

        Assert.Equal(0, _manager.BlacklistCount);
        Assert.False(_manager.IsBlacklisted(proxy));
    }

    [Fact]
    public void RemoveFromBlacklist_NonBlacklistedProxy_DoesNothing()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        _manager.RemoveFromBlacklist(proxy);

        Assert.Equal(0, _manager.BlacklistCount);
    }

    [Fact]
    public void IsBlacklisted_WithNullProxy_ReturnsTrue()
    {
        Assert.True(_manager.IsBlacklisted(null!));
    }

    [Fact]
    public void IsBlacklisted_NonBlacklistedProxy_ReturnsFalse()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        Assert.False(_manager.IsBlacklisted(proxy));
    }

    [Fact]
    public void Whitelist_WithNullProxy_DoesNothing()
    {
        _manager.Whitelist(null!);
        Assert.Equal(0, _manager.WhitelistCount);
    }

    [Fact]
    public void Whitelist_ValidProxy_AddsToWhitelist()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        _manager.Whitelist(proxy);

        Assert.Equal(1, _manager.WhitelistCount);
        Assert.True(_manager.IsWhitelisted(proxy));
    }

    [Fact]
    public void Whitelist_SameProxyTwice_DoesNotDuplicate()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        _manager.Whitelist(proxy);
        _manager.Whitelist(proxy);

        Assert.Equal(1, _manager.WhitelistCount);
    }

    [Fact]
    public void RemoveFromWhitelist_WhitelistedProxy_RemovesFromWhitelist()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        _manager.Whitelist(proxy);

        _manager.RemoveFromWhitelist(proxy);

        Assert.Equal(0, _manager.WhitelistCount);
        Assert.False(_manager.IsWhitelisted(proxy));
    }

    [Fact]
    public void IsWhitelisted_WithNullProxy_ReturnsFalse()
    {
        Assert.False(_manager.IsWhitelisted(null!));
    }

    [Fact]
    public void IsWhitelisted_NonWhitelistedProxy_ReturnsFalse()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");
        Assert.False(_manager.IsWhitelisted(proxy));
    }

    [Fact]
    public void GetWhitelistedProxies_ReturnsOnlyWhitelistedFromPool()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");
        ProxyInfo proxy3 = CreateTestProxy("http://test3:8080");

        _manager.Whitelist(proxy1);
        _manager.Whitelist(proxy3);

        var proxyPool = new Dictionary<string, ProxyInfo>
        {
            ["http://test1:8080|"] = proxy1,
            ["http://test2:8080|"] = proxy2,
            ["http://test3:8080|"] = proxy3
        };

        List<ProxyInfo> whitelisted = _manager.GetWhitelistedProxies(proxyPool);

        Assert.Equal(2, whitelisted.Count);
        Assert.Contains(proxy1, whitelisted);
        Assert.Contains(proxy3, whitelisted);
        Assert.DoesNotContain(proxy2, whitelisted);
    }

    [Fact]
    public void GetBlacklistedKeys_ReturnsAllBlacklistedKeys()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        _manager.Blacklist(proxy1);
        _manager.Blacklist(proxy2);

        IEnumerable<string> keys = _manager.GetBlacklistedKeys();

        Assert.Equal(2, keys.Count());
        Assert.Contains("http://test1:8080|", keys);
        Assert.Contains("http://test2:8080|", keys);
    }

    [Fact]
    public void GetWhitelistedKeys_ReturnsAllWhitelistedKeys()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        _manager.Whitelist(proxy1);
        _manager.Whitelist(proxy2);

        IEnumerable<string> keys = _manager.GetWhitelistedKeys();

        Assert.Equal(2, keys.Count());
        Assert.Contains("http://test1:8080|", keys);
        Assert.Contains("http://test2:8080|", keys);
    }

    [Fact]
    public void ClearBlacklist_RemovesAllBlacklistedProxies()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        _manager.Blacklist(proxy1);
        _manager.Blacklist(proxy2);
        _manager.ClearBlacklist();

        Assert.Equal(0, _manager.BlacklistCount);
        Assert.False(_manager.IsBlacklisted(proxy1));
        Assert.False(_manager.IsBlacklisted(proxy2));
    }

    [Fact]
    public void ClearWhitelist_RemovesAllWhitelistedProxies()
    {
        ProxyInfo proxy1 = CreateTestProxy("http://test1:8080");
        ProxyInfo proxy2 = CreateTestProxy("http://test2:8080");

        _manager.Whitelist(proxy1);
        _manager.Whitelist(proxy2);
        _manager.ClearWhitelist();

        Assert.Equal(0, _manager.WhitelistCount);
        Assert.False(_manager.IsWhitelisted(proxy1));
        Assert.False(_manager.IsWhitelisted(proxy2));
    }

    [Fact]
    public void Proxy_CanBeBothBlacklistedAndWhitelisted()
    {
        ProxyInfo proxy = CreateTestProxy("http://test:8080");

        _manager.Blacklist(proxy);
        _manager.Whitelist(proxy);

        Assert.True(_manager.IsBlacklisted(proxy));
        Assert.True(_manager.IsWhitelisted(proxy));
    }

    private static ProxyInfo CreateTestProxy(string server)
    {
        return new ProxyInfo(server, null, null);
    }
}
