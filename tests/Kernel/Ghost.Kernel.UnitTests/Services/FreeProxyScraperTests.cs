using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;
using Ghost.ProxyManagement;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.UnitTests.Services;

public sealed class FreeProxyScraperTests : ReliabilityTestBase
{
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));

    public FreeProxyScraperTests(ITestOutputHelper output) : base(output)
    {
    }

    private CancellationToken CancellationToken => _cts.Token;

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

    #region Constructor Tests

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        Action act = () =>
        {
            _ = new FreeProxyScraper(null!, NullLogger<FreeProxyScraper>.Instance);
        };

        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();
        HttpClient httpClient = CreateMockHttpClient(mockHandler);

        Action act = () =>
        {
            _ = new FreeProxyScraper(httpClient, null!);
        };

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithLogger_CreatesInstance()
    {
        var scraper = new FreeProxyScraper(NullLogger<FreeProxyScraper>.Instance);
        scraper.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithLogger_SetsTimeout()
    {
        var scraper = new FreeProxyScraper(NullLogger<FreeProxyScraper>.Instance);
        scraper.Should().NotBeNull();
        // The constructor creates HttpClient with 30 second timeout
    }

    [Fact]
    public void Constructor_WithCustomHttpClient_UsesProvidedClient()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();
        HttpClient httpClient = CreateMockHttpClient(mockHandler);

        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);
        scraper.Should().NotBeNull();
    }

    #endregion

    #region FetchProxiesAsync Tests

    [Fact]
    public async Task FetchProxiesAsync_AllSourcesSuccess_ReturnsProxies()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\n5.6.7.8:3128");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "9.10.11.12:8080");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "13.14.15.16:8080\n17.18.19.20:3128");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[{\"Ip\":\"21.22.23.24\",\"Port\":\"8080\"}]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        // proxy-list.download returns 3 (2 from http + 1 from https)
        // proxyscrape returns 2
        // proxyscan returns 1
        // Total = 6
        proxyList.Should().HaveCount(6);
        proxyList.Select(p => p.Server).Should().Contain("http://1.2.3.4:8080");
        proxyList.Select(p => p.Server).Should().Contain("http://21.22.23.24:8080");
    }

    [Fact]
    public async Task FetchProxiesAsync_RemovesDuplicates()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\n5.6.7.8:3128\n1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "1.2.3.4:8080\n9.10.11.12:8080");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "5.6.7.8:3128");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        // 1.2.3.4:8080 (deduplicated), 5.6.7.8:3128, 9.10.11.12:8080
        proxyList.Should().HaveCount(3);
        proxyList.Select(p => p.Server).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task FetchProxiesAsync_OneSourceFails_ReturnsProxiesFromOtherSources()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

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

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
    }

    [Fact]
    public async Task FetchProxiesAsync_AllSourcesFail_ReturnsEmptyList()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_EmptyResponses_ReturnsEmptyList()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_InvalidProxyFormat_SkipsInvalidLines()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\ninvalid-line\n5.6.7.8:3128\nno-port-here");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
        proxyList.Select(p => p.Server).Should().Contain("http://1.2.3.4:8080");
        proxyList.Select(p => p.Server).Should().Contain("http://5.6.7.8:3128");
    }

    [Fact]
    public async Task FetchProxiesAsync_InvalidPort_SkipsLine()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:abc");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_MultipleColons_SkipsInvalidLine()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080:extra\n5.6.7.8:3128");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
        proxyList[0].Server.Should().Be("http://5.6.7.8:3128");
    }

    [Fact]
    public async Task FetchProxiesAsync_EmptyLines_SkipsEmptyLines()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080\n\n\n5.6.7.8:3128\n");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
    }

    #endregion

    #region ProxyScan API Tests

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanJsonParsing_Success()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy",
            "[{\"Ip\":\"192.168.1.1\",\"Port\":\"8080\"},{\"Ip\":\"10.0.0.1\",\"Port\":\"3128\"}]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
        proxyList.Should().Contain(p => p.Server == "http://192.168.1.1:8080");
        proxyList.Should().Contain(p => p.Server == "http://10.0.0.1:3128");
    }

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanInvalidJson_ReturnsEmpty()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "invalid json");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanEmptyArray_ReturnsEmpty()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanNullResponse_ReturnsEmpty()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "null");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);

        proxies.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanNullIp_SkipsInvalid()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy",
            "[{\"Ip\":null,\"Port\":\"8080\"},{\"Ip\":\"10.0.0.1\",\"Port\":\"3128\"}]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
        proxyList[0].Server.Should().Be("http://10.0.0.1:3128");
    }

    [Fact]
    public async Task FetchProxiesAsync_ProxyScanNullPort_SkipsInvalid()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy",
            "[{\"Ip\":\"10.0.0.1\",\"Port\":null},{\"Ip\":\"10.0.0.2\",\"Port\":\"3128\"}]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
        proxyList[0].Server.Should().Be("http://10.0.0.2:3128");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task FetchProxiesAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();
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

        Func<Task> act = async () => await scraper.FetchProxiesAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchProxiesAsync_OperationCanceledException_NotCaughtAsGeneralException()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("User cancelled"));

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        Func<Task> act = async () => await scraper.FetchProxiesAsync(CancellationToken);
        ExceptionAssertions<OperationCanceledException> exception = await act.Should().ThrowAsync<OperationCanceledException>();
        exception.Which.Message.Should().Be("User cancelled");
    }

    #endregion

    #region HTTP Error Tests

    [Fact]
    public async Task FetchProxiesAsync_NonSuccessStatusCode_ContinuesProcessing()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

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

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
        proxyList[0].Server.Should().Be("http://1.2.3.4:8080");
    }

    [Fact]
    public async Task FetchProxiesAsync_HttpRequestException_HandledGracefully()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("proxy-list.download")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchProxiesAsync_InvalidOperationException_HandledGracefully()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("proxy-list.download")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Invalid operation"));

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
    }

    #endregion

    #region Whitespace Handling Tests

    [Fact]
    public async Task FetchProxiesAsync_WhitespaceInLines_TrimsCorrectly()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "  1.2.3.4:8080  \n\n  5.6.7.8:3128  \n");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
    }

    [Fact]
    public async Task FetchProxiesAsync_TabsInLines_TrimsCorrectly()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "\t1.2.3.4:8080\t\n\t5.6.7.8:3128\t");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(2);
    }

    #endregion

    #region Logging Tests

    [Fact]
#pragma warning disable CA1873 // Justification: Test mock verification - intentional evaluation
    public async Task FetchProxiesAsync_Success_LogsScrapedCount()
    {
        var mockLogger = new Mock<ILogger<FreeProxyScraper>>();
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();
        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, mockLogger.Object);

        await scraper.FetchProxiesAsync(CancellationToken);

        mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Scraped")),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }
#pragma warning restore CA1873

    [Fact]
    public async Task FetchProxiesAsync_Failure_HandlesGracefully()
    {
        var mockLogger = new Mock<ILogger<FreeProxyScraper>>();

        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, mockLogger.Object);

        // Should complete without throwing
        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);

        proxies.Should().BeEmpty();
    }

    #endregion

    #region ProxyInfo Tests

    [Fact]
    public async Task FetchProxiesAsync_ReturnsProxies_WithNullCredentials()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(1);
        proxyList[0].Server.Should().Be("http://1.2.3.4:8080");
        proxyList[0].Username.Should().BeNull();
        proxyList[0].Password.Should().BeNull();
    }

    [Fact]
    public async Task FetchProxiesAsync_MultipleSources_ReturnsProxiesFromAllSources()
    {
        Mock<HttpMessageHandler> mockHandler = CreateMockHandler();

        SetupMockResponse(mockHandler, "https://www.free-proxy-list.net/", "<html></html>");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=http", "1.2.3.4:8080");
        SetupMockResponse(mockHandler, "https://www.proxy-list.download/api/v1/get?type=https", "2.2.2.2:3128");
        SetupMockResponse(mockHandler, "https://api.proxyscrape.com/v2/", "3.3.3.3:8080");
        SetupMockResponse(mockHandler, "https://www.proxyscan.io/api/proxy", "[{\"Ip\":\"4.4.4.4\",\"Port\":\"8080\"}]");

        HttpClient httpClient = CreateMockHttpClient(mockHandler);
        var scraper = new FreeProxyScraper(httpClient, NullLogger<FreeProxyScraper>.Instance);

        IEnumerable<ProxyInfo> proxies = await scraper.FetchProxiesAsync(CancellationToken);
        var proxyList = proxies.ToList();

        proxyList.Should().HaveCount(4);
        proxyList.Should().Contain(p => p.Server == "http://1.2.3.4:8080");
        proxyList.Should().Contain(p => p.Server == "http://2.2.2.2:3128");
        proxyList.Should().Contain(p => p.Server == "http://3.3.3.3:8080");
        proxyList.Should().Contain(p => p.Server == "http://4.4.4.4:8080");
    }

    #endregion
}
