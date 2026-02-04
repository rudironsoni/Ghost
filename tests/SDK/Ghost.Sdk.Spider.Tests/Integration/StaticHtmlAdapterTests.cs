using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using WireMock.Server;
using WireMockRequest = WireMock.RequestBuilders.Request;
using WireMockResponse = WireMock.ResponseBuilders.Response;

namespace Ghost.Sdk.Spider.Tests.Integration;

/// <summary>
/// Integration tests for StaticHtmlAdapter using WireMock.Net for HTTP mocking.
/// </summary>
[TestFixture]
public class StaticHtmlAdapterTests
{
    private WireMockServer _server = null!;
    private HttpClient _httpClient = null!;
    private StaticHtmlAdapter _adapter = null!;

    [SetUp]
    public void Setup()
    {
        _server = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        _adapter = new StaticHtmlAdapter(_httpClient, NullLogger<StaticHtmlAdapter>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    #region HTTP GET Requests

    [Test]
    public async Task ExtractAsync_WithSimpleGetRequest_ShouldReturnHtmlContent()
    {
        // Arrange
        const string expectedContent = "<html><body><h1>Hello World</h1></body></html>";
        _server
            .Given(WireMockRequest.Create().WithPath("/page").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(expectedContent));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/page",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(200);
        response.Content.Content.Should().Be(expectedContent);
        response.Content.ContentType.Should().Be(ContentType.StaticHtml);
        response.AdapterName.Should().Be("StaticHtml");
        response.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Test]
    public async Task ExtractAsync_WithQueryParameters_ShouldSendCorrectUrl()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create()
                .WithPath("/search")
                .WithParam("q", "test")
                .WithParam("limit", "10")
                .UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("<html><body>Search results</body></html>"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/search?q=test&limit=10",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(200);
        response.Content.Content.Should().Contain("Search results");
    }

    #endregion

    #region Custom Headers

    [Test]
    public async Task ExtractAsync_WithCustomHeaders_ShouldSendHeaders()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create()
                .WithPath("/api")
                .WithHeader("X-API-Key", "secret123")
                .WithHeader("X-Client-Version", "1.0.0")
                .UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Authenticated"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/api",
            Method = "GET",
            Headers = new Dictionary<string, string>
            {
                { "X-API-Key", "secret123" },
                { "X-Client-Version", "1.0.0" }
            },
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Be("Authenticated");
    }

    [Test]
    public async Task ExtractAsync_WithAuthorizationHeader_ShouldAuthenticate()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create()
                .WithPath("/protected")
                .WithHeader("Authorization", "Bearer token123")
                .UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Protected content"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/protected",
            Method = "GET",
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token123" }
            },
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Be("Protected content");
    }

    #endregion

    #region Cookie Handling

    [Test]
    public async Task ExtractAsync_WithCookies_ShouldSendCookieHeader()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create()
                .WithPath("/session")
                .WithHeader("Cookie", "session_id=abc123; user_pref=dark_mode")
                .UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Session active"));

        var options = new StaticHtmlAdapterOptions
        {
            Cookies = new Dictionary<string, string>
            {
                { "session_id", "abc123" },
                { "user_pref", "dark_mode" }
            }
        };

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/session",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request, options);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Be("Session active");
    }

    [Test]
    public async Task ExtractAsync_WithSetCookieResponse_ShouldReceiveCookies()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/login").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Set-Cookie", "session_id=xyz789; Path=/; HttpOnly")
                .WithBody("Login successful"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/login",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Headers.Should().ContainKey("Set-Cookie");
        response.Headers["Set-Cookie"].Should().Contain("session_id=xyz789");
    }

    #endregion

    #region Timeout Handling

    [Test]
    public async Task ExtractAsync_WithSlowResponse_ShouldTimeout()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/slow").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Delayed response")
                .WithDelay(TimeSpan.FromSeconds(5)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/slow",
            Method = "GET",
            Timeout = TimeSpan.FromMilliseconds(100)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("timed out");
    }

    [Test]
    public async Task ExtractAsync_WithCancellationToken_ShouldCancelRequest()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/longrunning").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Long running")
                .WithDelay(TimeSpan.FromSeconds(10)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/longrunning",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var response = await _adapter.ExtractAsync(request, cts.Token);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("canceled");
    }

    #endregion

    #region Redirect Following

    [Test]
    public async Task ExtractAsync_WithSingleRedirect_ShouldFollowRedirect()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/redirect").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(302)
                .WithHeader("Location", $"{_server.Url}/target"));

        _server
            .Given(WireMockRequest.Create().WithPath("/target").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Final destination"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/redirect",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Be("Final destination");
        response.FinalUrl.Should().Contain("/target");
    }

    [Test]
    public async Task ExtractAsync_WithMultipleRedirects_ShouldFollowChain()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/redirect1").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(301)
                .WithHeader("Location", $"{_server.Url}/redirect2"));

        _server
            .Given(WireMockRequest.Create().WithPath("/redirect2").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(302)
                .WithHeader("Location", $"{_server.Url}/final"));

        _server
            .Given(WireMockRequest.Create().WithPath("/final").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Final page"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/redirect1",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Be("Final page");
        response.RedirectCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Compression (gzip/deflate)

    [Test]
    public async Task ExtractAsync_WithGzipCompression_ShouldDecompress()
    {
        // Arrange
        const string content = "This is compressed content that should be decompressed automatically";
        _server
            .Given(WireMockRequest.Create().WithPath("/compressed").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "gzip")
                .WithBody(content));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/compressed",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().NotBeEmpty();
    }

    [Test]
    public async Task ExtractAsync_WithDeflateCompression_ShouldDecompress()
    {
        // Arrange
        const string content = "Deflate compressed content";
        _server
            .Given(WireMockRequest.Create().WithPath("/deflate").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Encoding", "deflate")
                .WithBody(content));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/deflate",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().NotBeEmpty();
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task ExtractAsync_With404NotFound_ShouldReturnErrorResponse()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/notfound").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(404)
                .WithBody("Not Found"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/notfound",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(404);
        response.Content.Success.Should().BeFalse();
    }

    [Test]
    public async Task ExtractAsync_With500ServerError_ShouldReturnErrorResponse()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/error").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(500)
                .WithBody("Internal Server Error"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/error",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(500);
    }

    [Test]
    public async Task ExtractAsync_WithInvalidUrl_ShouldReturnErrorResponse()
    {
        // Arrange
        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = "http://localhost:99999/invalid",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(1)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region POST Requests

    [Test]
    public async Task ExtractAsync_WithPostRequest_ShouldSendBodyAndHeaders()
    {
        // Arrange
        const string postBody = "{\"username\":\"test\",\"password\":\"secret\"}";
        _server
            .Given(WireMockRequest.Create()
                .WithPath("/api/login")
                .UsingPost()
                .WithBody(postBody)
                .WithHeader("Content-Type", "application/json"))
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("{\"token\":\"abc123\"}"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/api/login",
            Method = "POST",
            Body = postBody,
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            },
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("token");
    }

    [Test]
    public async Task ExtractAsync_WithFormDataPost_ShouldSendFormData()
    {
        // Arrange
        const string formData = "name=John&email=john@example.com";
        _server
            .Given(WireMockRequest.Create()
                .WithPath("/submit")
                .UsingPost()
                .WithBody(formData))
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Form submitted"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/submit",
            Method = "POST",
            Body = formData,
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/x-www-form-urlencoded" }
            },
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Be("Form submitted");
    }

    #endregion

    #region Connection Reuse

    [Test]
    public async Task ExtractAsync_WithMultipleRequests_ShouldReuseConnection()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/test").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Test content"));

        var requests = Enumerable.Range(1, 5).Select(i => new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/test",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        }).ToList();

        // Act
        var responses = new List<Response>();
        foreach (var req in requests)
        {
            responses.Add(await _adapter.ExtractAsync(req));
        }

        // Assert
        responses.Should().HaveCount(5);
        responses.Should().OnlyContain(r => r.IsSuccess);
        _server.LogEntries.Should().HaveCount(5);
    }

    #endregion

    #region Response Headers

    [Test]
    public async Task ExtractAsync_WithResponseHeaders_ShouldCaptureHeaders()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/headers").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("X-Custom-Header", "CustomValue")
                .WithHeader("X-Request-Id", "req-123")
                .WithBody("Content"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/headers",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Headers.Should().ContainKey("X-Custom-Header");
        response.Headers["X-Custom-Header"].Should().Be("CustomValue");
        response.Headers.Should().ContainKey("X-Request-ID");
    }

    #endregion

    #region Content Type Detection

    [Test]
    public async Task ExtractAsync_WithJsonContentType_ShouldDetectContentType()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/api/data").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"key\":\"value\"}"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/api/data",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.MimeType.Should().Be("application/json");
    }

    [Test]
    public async Task ExtractAsync_WithXmlContentType_ShouldDetectContentType()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/feed.xml").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/xml")
                .WithBody("<?xml version=\"1.0\"?><root><item>data</item></root>"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/feed.xml",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.MimeType.Should().Be("application/xml");
    }

    #endregion
}
