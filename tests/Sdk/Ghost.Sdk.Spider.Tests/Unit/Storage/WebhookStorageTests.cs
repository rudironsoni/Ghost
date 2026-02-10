using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Ghost.Sdk.Spider.Storage.Sinks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;
using System.Net;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

public class WebhookStorageTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly WebhookStorage _storage;
    private const string WebhookUrl = "https://webhook.example.com/receive";

    public WebhookStorageTests()
    {
        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandler.Object);
        _storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ShouldThrow()
    {
        // Act
        var act = () => new WebhookStorage(null!, WebhookUrl);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_WithNullWebhookUrl_ShouldThrow()
    {
        // Act
        var act = () => new WebhookStorage(_httpClient, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("webhookUrl");
    }

    [Fact]
    public void Name_ShouldReturnWebhook()
    {
        // Act
        var name = _storage.Name;

        // Assert
        name.Should().Be("Webhook");
    }

    [Fact]
    public void IsAvailable_WithValidUrl_ShouldReturnTrue()
    {
        // Act
        var isAvailable = _storage.IsAvailable;

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WithEmptyUrl_ShouldReturnFalse()
    {
        // Arrange
        var storage = new WebhookStorage(_httpClient, string.Empty);

        // Act
        var isAvailable = storage.IsAvailable;

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldComplete()
    {
        // Act
        var act = async () => await _storage.InitializeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StoreAsync_WithSuccessResponse_ShouldReturnSuccess()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Success")
            });

        var item = new { Name = "Test", Value = 42 };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            SourceUrl = "https://example.com",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task StoreAsync_WithErrorResponse_ShouldReturnFailure()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                ReasonPhrase = "Server Error"
            });

        var item = new { Name = "Test", Value = 42 };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            SourceUrl = "https://example.com",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("InternalServerError");
        result.Error.Should().Contain("Server Error");
    }

    [Fact]
    public async Task StoreAsync_WithException_ShouldReturnFailure()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var item = new { Name = "Test", Value = 42 };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            SourceUrl = "https://example.com",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Network error");
        result.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task StoreBatchAsync_WithSuccessResponse_ShouldReturnSuccess()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Success")
            });

        var items = new[]
        {
            new { Name = "Item1", Value = 1 },
            new { Name = "Item2", Value = 2 },
            new { Name = "Item3", Value = 3 }
        };
        var context = new StorageContext
        {
            SpiderName = "BatchSpider",
            SourceUrl = "https://example.com/batch",
            Timestamp = DateTimeOffset.UtcNow,
            BatchId = "batch-123"
        };

        // Act
        var result = await _storage.StoreBatchAsync(items, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);
    }

    [Fact]
    public async Task StoreBatchAsync_WithEmptyList_ShouldSucceed()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var items = Array.Empty<object>();
        var context = new StorageContext
        {
            SpiderName = "EmptyBatchSpider",
            SourceUrl = "https://example.com",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreBatchAsync(items, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(0);
    }

    [Fact]
    public async Task FlushAsync_ShouldComplete()
    {
        // Act
        var act = async () => await _storage.FlushAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CloseAsync_ShouldComplete()
    {
        // Act
        var act = async () => await _storage.CloseAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StoreAsync_ShouldSendCorrectPayload()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var item = new { Name = "Test", Value = 42 };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            SourceUrl = "https://example.com",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object> { ["key"] = "value" },
            Tags = new List<string> { "tag1", "tag2" }
        };

        // Act
        await _storage.StoreAsync(item, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri.Should().Be(WebhookUrl);
        capturedRequest.Content.Should().NotBeNull();

        var content = await capturedRequest.Content!.ReadAsStringAsync();
        content.Should().Contain("TestSpider");
        content.Should().Contain("https://example.com");
        content.Should().Contain("Test");
        content.Should().Contain("42");
        content.Should().Contain("tag1");
        content.Should().Contain("key");
    }
}
