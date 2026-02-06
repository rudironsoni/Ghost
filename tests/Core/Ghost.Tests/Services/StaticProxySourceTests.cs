using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Core;
using Ghost.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Tests.Services;

public class StaticProxySourceTests
{
    [Fact]
    public async Task FetchProxiesAsyncShouldUseGlobalConfigWhenItemIsBareHost()
    {
        // With the new configuration model, global Port fallback for bare hosts is removed.
        var cfg = new ProxySourceConfig
        {
            Enabled = true,
            Username = "u",
            Password = "p",
            Hosts = { "bare.host" }
        };

        var sut = new StaticProxySource(cfg, NullLogger<StaticProxySource>.Instance);
        var res = (await sut.FetchProxiesAsync(CancellationToken.None)).ToList();

        // bare.host (no scheme or port) is ignored without a global port
        res.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsyncShouldPrioritizeItemConfigWhenItemHasFullUrl()
    {
        var cfg = new ProxySourceConfig
        {
            Enabled = true,
            Username = "u",
            Password = "p",
            Hosts = { "socks5://custom:pass@host:9999" }
        };

        var sut = new StaticProxySource(cfg, NullLogger<StaticProxySource>.Instance);
        var res = (await sut.FetchProxiesAsync(CancellationToken.None)).ToList();

        res.Should().HaveCount(1);
        res[0].Server.Should().Be("socks5://host:9999");
        res[0].Username.Should().Be("custom");
        res[0].Password.Should().Be("pass");
    }

    [Fact]
    public async Task FetchProxiesAsyncShouldEnrichItemWhenItemMissingAuth()
    {
        var cfg = new ProxySourceConfig
        {
            Enabled = true,
            Username = "u",
            Password = "p",
            Hosts = { "host:1234" }
        };

        var sut = new StaticProxySource(cfg, NullLogger<StaticProxySource>.Instance);
        var res = (await sut.FetchProxiesAsync(CancellationToken.None)).ToList();

        res.Should().HaveCount(1);
        res[0].Server.Should().Be("http://host:1234");
        res[0].Username.Should().Be("u");
        res[0].Password.Should().Be("p");
    }

    [Fact]
    public async Task FetchProxiesAsyncShouldSkipInvalidItems()
    {
        var cfg = new ProxySourceConfig
        {
            Enabled = true,
            Hosts = { "invalid_proxy_string" }
        };

        var sut = new StaticProxySource(cfg, NullLogger<StaticProxySource>.Instance);
        var res = (await sut.FetchProxiesAsync(CancellationToken.None)).ToList();

        res.Should().BeEmpty();
    }
}
