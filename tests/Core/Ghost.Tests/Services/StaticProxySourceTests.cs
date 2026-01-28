using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Ghost.Core;
using Ghost.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace Ghost.Tests.Services;

public class StaticProxySourceTests
{
        [Fact]
        public async Task FetchProxiesAsync_ShouldUseGlobalConfig_WhenItemIsBareHost()
        {
            var options = new ProxyOptions
            {
                Static = new StaticProxyConfig
                {
                    Enabled = true,
                    Port = 1080,
                    Username = "u",
                    Password = "p",
                    Items = { "bare.host" }
                }
            };

            var moq = new Mock<IOptions<ProxyOptions>>();
            moq.Setup(x => x.Value).Returns(options);

            var sut = new StaticProxySource(moq.Object, NullLogger<StaticProxySource>.Instance);
            var res = (await sut.FetchProxiesAsync(CancellationToken.None)).ToList();

            res.Should().HaveCount(1);
            res[0].Server.Should().Be("bare.host:1080");
            res[0].Username.Should().Be("u");
            res[0].Password.Should().Be("p");
        }

        [Fact]
        public async Task FetchProxiesAsync_ShouldPrioritizeItemConfig_WhenItemHasFullUrl()
        {
            var options = new ProxyOptions
            {
                Static = new StaticProxyConfig
                {
                    Enabled = true,
                    Port = 1080,
                    Username = "u",
                    Password = "p",
                    Items = { "socks5://custom:pass@host:9999" }
                }
            };

            var moq = new Mock<IOptions<ProxyOptions>>();
            moq.Setup(x => x.Value).Returns(options);

            var sut = new StaticProxySource(moq.Object, NullLogger<StaticProxySource>.Instance);
            var res = (await sut.FetchProxiesAsync(CancellationToken.None)).ToList();

            res.Should().HaveCount(1);
            res[0].Server.Should().Be("host:9999");
            res[0].Username.Should().Be("custom");
            res[0].Password.Should().Be("pass");
        }

        [Fact]
        public async Task FetchProxiesAsync_ShouldEnrichItem_WhenItemMissingAuth()
        {
            var options = new ProxyOptions
            {
                Static = new StaticProxyConfig
                {
                    Enabled = true,
                    Port = 1080,
                    Username = "u",
                    Password = "p",
                    Items = { "host:1234" }
                }
            };

            var moq = new Mock<IOptions<ProxyOptions>>();
            moq.Setup(x => x.Value).Returns(options);

            var sut = new StaticProxySource(moq.Object, NullLogger<StaticProxySource>.Instance);
            var res = (await sut.FetchProxiesAsync(CancellationToken.None)).ToList();

            res.Should().HaveCount(1);
            res[0].Server.Should().Be("host:1234");
            res[0].Username.Should().Be("u");
            res[0].Password.Should().Be("p");
        }

        [Fact]
        public async Task FetchProxiesAsync_ShouldSkipInvalidItems()
        {
            var options = new ProxyOptions
            {
                Static = new StaticProxyConfig
                {
                    Enabled = true,
                    Items = { "invalid_proxy_string" }
                }
            };

            var moq = new Mock<IOptions<ProxyOptions>>();
            moq.Setup(x => x.Value).Returns(options);

            var sut = new StaticProxySource(moq.Object, NullLogger<StaticProxySource>.Instance);
            var res = (await sut.FetchProxiesAsync(CancellationToken.None)).ToList();

            res.Should().BeEmpty();
        }
}
