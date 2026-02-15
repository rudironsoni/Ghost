using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Ghost.Tests.ProxyManagement;

public sealed class FreeProxyScraperTests
{
    private static HttpClient CreateMockHttpClient(Mock<HttpMessageHandler> mockHandler)
    {
        return new HttpClient(mockHandler.Object);
    }

    private static void SetupMockResponse(Mock<HttpMessageHandler> mockHandler, string url, string content)
    {
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().StartsWith(url)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });
    }

    [Fact]
    public async Task FetchProxiesAsync_ReturnsProxies()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        // Setup mock responses for each proxy source
        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\n5.6.7.8:3128");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "9.10.11.12:8080");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "13.14.15.16:8080\n17.18.19.20:3128");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[{\"Ip\":\"21.22.23.24\",\"Port\":\"8080\"}]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        // Act
        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken.None);

        // Assert
        proxies.Should().NotBeNull();
        proxies.Should().HaveCountGreaterThan(0, "should parse proxies from mock responses");
    }

    [Fact]
    public async Task FetchProxiesAsync_RemovesDuplicates()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        // Setup mock responses with duplicate proxies
        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\n5.6.7.8:3128\n1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "1.2.3.4:8080\n9.10.11.12:8080");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "5.6.7.8:3128");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        // Act
        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        // Assert
        int uniqueServers = proxyList.Select(p => p.Server).Distinct().Count();
        uniqueServers.Should().Be(proxyList.Count, "all proxies should have unique server addresses");
        proxyList.Should().HaveCount(3, "should have deduplicated the 5 mock proxies down to 3 unique ones");
    }

    [Fact]
    public async Task FetchProxiesAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await scraper.FetchProxiesAsync(cts.Token).ConfigureAwait(false));
    }
}
