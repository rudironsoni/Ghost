using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using WireMockRequest = WireMock.RequestBuilders.Request;
using WireMockResponse = WireMock.ResponseBuilders.Response;
using WireMock.Server;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

public class StaticHtmlAdapterTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _httpClient;
    private readonly StaticHtmlAdapter _adapter;

    public StaticHtmlAdapterTests()
    {
        _server = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        _adapter = new StaticHtmlAdapter(_httpClient, NullLogger<StaticHtmlAdapter>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _server.Stop();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ExtractAsync_WithSuccessfulResponse_ShouldReturnResponse()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/test").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("<html><body>Test Content</body></html>")
                .WithHeader("Content-Type", "text/html"));

        var request = TestData.CreateRequest($"{_server.Url}/test");

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(200);
        response.Content.Content.Should().Contain("Test Content");
    }

    [Fact]
    public async Task ExtractAsync_With404_ShouldReturnErrorResponse()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/notfound").UsingGet())
            .RespondWith(WireMockResponse.Create().WithStatusCode(404));

        var request = TestData.CreateRequest($"{_server.Url}/notfound");

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ExtractAsync_WithCustomHeaders_ShouldSendHeaders()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create()
                .WithPath("/test")
                .WithHeader("X-Custom-Header", "CustomValue"))
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Success"));

        var request = TestData.CreateRequest($"{_server.Url}/test");
        request.Headers["X-Custom-Header"] = "CustomValue";

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithPostRequest_ShouldSendBody()
    {
        // Arrange
        var expectedBody = "{\"key\": \"value\"}";
        _server
            .Given(WireMockRequest.Create()
                .WithPath("/api")
                .UsingPost()
                .WithBody(expectedBody))
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("{\"result\": \"ok\"}"));

        var request = TestData.CreateRequest($"{_server.Url}/api", "POST", body: expectedBody);
        request.Headers["Content-Type"] = "application/json";

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithTimeout_ShouldThrowTimeout()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/slow").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Delayed")
                .WithDelay(TimeSpan.FromSeconds(10)));

        var request = TestData.CreateRequest($"{_server.Url}/slow");
        request.Timeout = TimeSpan.FromMilliseconds(100);

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("timed out");
    }

    [Fact]
    public async Task ExtractAsync_WithCancellation_ShouldCancelRequest()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/long").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Content")
                .WithDelay(TimeSpan.FromSeconds(10)));

        var request = TestData.CreateRequest($"{_server.Url}/long");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var response = await _adapter.ExtractAsync(request, cts.Token);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("canceled");
    }

    [Fact]
    public async Task CanHandleAsync_WithHttpUrl_ShouldReturnTrue()
    {
        // Arrange
        var request = TestData.CreateRequest("https://example.com");

        // Act
        var canHandle = await _adapter.CanHandleAsync(request);

        // Assert
        canHandle.Should().BeTrue();
    }

    [Fact]
    public async Task CanHandleAsync_WithNonHttpUrl_ShouldReturnFalse()
    {
        // Arrange
        var request = TestData.CreateRequest("ftp://example.com");

        // Act
        var canHandle = await _adapter.CanHandleAsync(request);

        // Assert
        canHandle.Should().BeFalse();
    }

    [Fact]
    public void Name_ShouldBeStaticHtml()
    {
        // Assert
        _adapter.Name.Should().Be("StaticHtml");
    }

    [Fact]
    public void ContentType_ShouldBeStaticHtml()
    {
        // Assert
        _adapter.ContentType.Should().Be(ContentType.StaticHtml);
    }

    [Fact]
    public void IsAvailable_ShouldBeTrue()
    {
        // Assert
        _adapter.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithRedirect_ShouldFollowRedirect()
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
                .WithBody("Final Content"));

        var request = TestData.CreateRequest($"{_server.Url}/redirect");

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("Final Content");
    }

    [Fact]
    public async Task ExtractAsync_WithGzipEncoding_ShouldDecompress()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/compressed").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Compressed Content")
                .WithHeader("Content-Encoding", "gzip"));

        var request = TestData.CreateRequest($"{_server.Url}/compressed");

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithMultipleRequests_ShouldReuseConnection()
    {
        // Arrange
        _server
            .Given(WireMockRequest.Create().WithPath("/test").UsingGet())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithBody("Content"));

        var request1 = TestData.CreateRequest($"{_server.Url}/test");
        var request2 = TestData.CreateRequest($"{_server.Url}/test");

        // Act
        var response1 = await _adapter.ExtractAsync(request1);
        var response2 = await _adapter.ExtractAsync(request2);

        // Assert
        response1.IsSuccess.Should().BeTrue();
        response2.IsSuccess.Should().BeTrue();
        _server.LogEntries.Should().HaveCount(2);
    }
}
