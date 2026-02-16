using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Ghost.Kernel.UnitTests.Services;

public sealed class FreeProxyScraperTests
{
    private static Mock<HttpMessageHandler> CreateMockHandler()
    {
        return new Mock<HttpMessageHandler>();
    }

    private static HttpClient CreateMockHttpClient(Mock<HttpMessageHandler> mockHandler)
    {
        return new HttpClient(mockHandler.Object);
    }

    private static void SetupMockResponse(Mock<HttpMessageHandler> mockHandler, string url, string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().StartsWith(url, StringComparison.OrdinalIgnoreCase)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    private static void SetupMockResponseForAnyRequest(Mock<HttpMessageHandler> mockHandler, string content)
    {
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        Action act = () => new FreeProxyScraper(null!, NullLogger<FreeProxyScraper>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var mockHandler = CreateMockHandler();
        var httpClient = CreateMockHttpClient(mockHandler);

        Action act = () => new FreeProxyScraper(httpClient, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithLogger_CreatesInstance()
    {
        var scraper = new FreeProxyScraper(NullLogger<FreeProxyScraper>.Instance);
        scraper.Should().NotBeNull();
    }

    #endregion

    #region FetchProxiesAsync Tests

    [Fact]
    public async Task FetchProxiesAsync_AllSourcesSuccess_ReturnsProxies()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\n5.6.7.8:3128");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "9.10.11.12:8080");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "13.14.15.16:8080\n17.18.19.20:3128");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[{\"Ip\":\"21.22.23.24\",\"Port\":\"8080\"}]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(5);
        proxyList.Select(p => p.Server).Should().Contain("http://1.2.3.4:8080");
        proxyList.Select(p => p.Server).Should().Contain("http://21.22.23.24:8080");
    }

    [Fact]
    public async Task FetchProxiesAsync_RemovesDuplicates()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\n5.6.7.8:3128\n1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "1.2.3.4:8080\n9.10.11.12:8080");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "5.6.7.8:3128");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(3);
        proxyList.Select(p => p.Server).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task FetchProxiesAsync_OneSourceFails_ReturnsProxiesFromOtherSources()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "5.6.7.8:3128");

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("proxyscrape")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
    }

    [Fact]
    public async Task FetchProxiesAsync_AllSourcesFail_ReturnsEmptyList()
    {
        var mockHandler = CreateMockHandler();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_EmptyResponses_ReturnsEmptyList()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_InvalidProxyFormat_SkipsInvalidLines()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\ninvalid-line\n5.6.7.8:3128\nno-port-here");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
        proxyList.Select(p => p.Server).Should().Contain("http://1.2.3.4:8080");
        proxyList.Select(p => p.Server).Should().Contain("http://5.6.7.8:3128");
    }

    [Fact]
    public async Task FetchProxiesAsync_InvalidPort_SkipsLine()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:abc");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);

        proxies.Should().BeEmpty();
    }

    #endregion

    #region ProxyScan API Tests

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanJsonParsing_Success()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy",
            "[{\"Ip\":\"192.168.1.1\",\"Port\":\"8080\"},{\"Ip\":\"10.0.0.1\",\"Port\":\"3128\"}]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
        proxyList.Should().Contain(p => p.Server == "http://192.168.1.1:8080");
        proxyList.Should().Contain(p => p.Server == "http://10.0.0.1:3128");
    }

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanInvalidJson_ReturnsEmpty()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "invalid json");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanEmptyArray_ReturnsEmpty()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);

        proxies.Should().BeEmpty();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task FetchProxiesAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        var mockHandler = CreateMockHandler();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await scraper.FetchProxiesAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchProxiesAsync_OperationCanceledException_NotCaughtAsGeneralException()
    {
        var mockHandler = CreateMockHandler();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("User cancelled"));

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        Func<Task> act = async () => await scraper.FetchProxiesAsync(CancellationToken.None);
        var exception = await act.Should().ThrowAsync<OperationCanceledException>();
        exception.Which.Message.Should().Be("User cancelled");
    }

    #endregion

    #region HTTP Error Tests

    [Fact]
    public async Task FetchProxiesAsync_NonSuccessStatusCode_ContinuesProcessing()
    {
        var mockHandler = CreateMockHandler();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("free-proxy-list")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("Error")
            });

        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
        proxyList[0].Server.Should().Be("http://1.2.3.4:8080");
    }

    [Fact]
    public async Task FetchProxiesAsync_TimeoutException_HandledGracefully()
    {
        var mockHandler = CreateMockHandler();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("proxy-list.download")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
    }

    #endregion

    #region Whitespace Handling Tests

    [Fact]
    public async Task FetchProxiesAsync_WhitespaceInLines_TrimsCorrectly()
    {
        var mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "  1.2.3.4:8080  \n\n  5.6.7.8:3128  \n");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        var httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        var proxies = await scraper.FetchProxiesAsync(CancellationToken.None);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
    }

    #endregion
}
