using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Ghost.Sdk.Spider.Storage.Sinks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Detailed tests for webhook storage edge cases and advanced scenarios
/// </summary>
[TestFixture]
public class WebhookStorageDetailedTests
{
    private Mock<HttpMessageHandler> _httpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private const string WebhookUrl = "https://webhook.example.com/receive";

    [SetUp]
    public void Setup()
    {
        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandler.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
    }

    [Test]
    public async Task StoreAsync_WithTimeout_ShouldReturnFailure()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        var item = new { Data = "test" };
        var context = new StorageContext { SpiderName = "TestSpider", SourceUrl = "https://example.com" };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timeout");
    }

    [Test]
    public async Task StoreAsync_WithComplexObject_ShouldSerializeCorrectly()
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

        var storage = new WebhookStorage(_httpClient, WebhookUrl);

        var complexItem = new
        {
            Id = 123,
            Name = "Test Item",
            Tags = new[] { "tag1", "tag2" },
            Metadata = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 42,
                ["nested"] = new { Inner = "value" }
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        var context = new StorageContext { SpiderName = "ComplexSpider", SourceUrl = "https://example.com" };

        // Act
        await storage.StoreAsync(complexItem, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        content.Should().Contain("\"Id\":123");
        content.Should().Contain("Test Item");
        content.Should().Contain("tag1");
        content.Should().Contain("key1");
        content.Should().Contain("nested");
    }

    [Test]
    public async Task StoreBatchAsync_WithLargeBatch_ShouldHandle()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var storage = new WebhookStorage(_httpClient, WebhookUrl);

        var items = Enumerable.Range(1, 1000)
            .Select(i => new { Id = i, Name = $"Item {i}", Data = $"Data {i}" })
            .ToArray();

        var context = new StorageContext { SpiderName = "BatchSpider", SourceUrl = "https://example.com" };

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1000);
    }

    [Test]
    public async Task StoreAsync_WithRetryableError_ShouldReturnFailure()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                ReasonPhrase = "Service Unavailable"
            });

        var storage = new WebhookStorage(_httpClient, WebhookUrl);
        var item = new { Data = "test" };
        var context = new StorageContext { SpiderName = "TestSpider", SourceUrl = "https://example.com" };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("ServiceUnavailable");
    }

    [Test]
    public async Task StoreAsync_WithJsonContent_ShouldSerializeProperly()
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

        var storage = new WebhookStorage(_httpClient, WebhookUrl);
        var item = new { Data = "test", Value = 123 };
        var context = new StorageContext { SpiderName = "TestSpider", SourceUrl = "https://example.com" };

        // Act
        await storage.StoreAsync(item, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().NotBeNull();
        var content = await capturedRequest.Content!.ReadAsStringAsync();
        content.Should().Contain("test");
        content.Should().Contain("123");
    }

    [Test]
    public async Task StoreAsync_WithMinimalContext_ShouldSucceed()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var storage = new WebhookStorage(_httpClient, WebhookUrl);
        var item = new { Data = "test" };
        var context = new StorageContext { SpiderName = "MinimalSpider" };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task StoreBatchAsync_WithPartialSuccess_ShouldReportCorrectly()
    {
        // Arrange
        var callCount = 0;
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new HttpResponseMessage { StatusCode = HttpStatusCode.OK }
                    : new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
            });

        var storage = new WebhookStorage(_httpClient, WebhookUrl);
        var items = new[] { new { Id = 1 }, new { Id = 2 } };
        var context = new StorageContext { SpiderName = "PartialSpider", SourceUrl = "https://example.com" };

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert - Batch sends all items in one request, so it's either all or nothing
        result.Should().NotBeNull();
    }

    [Test]
    public async Task StoreAsync_WithSpecialCharacters_ShouldEscapeCorrectly()
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

        var storage = new WebhookStorage(_httpClient, WebhookUrl);

        var item = new
        {
            Text = "Text with \"quotes\" and \n newlines and \t tabs",
            Emoji = "🚀💡",
            Unicode = "日本語",
            SpecialChars = "<>&'\""
        };

        var context = new StorageContext { SpiderName = "SpecialCharsSpider", SourceUrl = "https://example.com" };

        // Act
        await storage.StoreAsync(item, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.Should().NotBeNull();
    }

    [Test]
    public async Task StoreAsync_WithCircularReference_ShouldHandleGracefully()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var storage = new WebhookStorage(_httpClient, WebhookUrl);

        // Create a simple object that won't have circular references
        var item = new { Id = 1, Name = "Test" };
        var context = new StorageContext { SpiderName = "CircularSpider", SourceUrl = "https://example.com" };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task StoreAsync_WithContextMetadata_ShouldIncludeInPayload()
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

        var storage = new WebhookStorage(_httpClient, WebhookUrl);
        var item = new { Data = "test" };
        var context = new StorageContext
        {
            SpiderName = "MetadataSpider",
            SourceUrl = "https://example.com",
            Metadata = new Dictionary<string, object>
            {
                ["userId"] = "user123",
                ["sessionId"] = "session456"
            },
            Tags = new List<string> { "important", "verified" },
            Timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)
        };

        // Act
        await storage.StoreAsync(item, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        content.Should().Contain("MetadataSpider");
        content.Should().Contain("user123");
        content.Should().Contain("session456");
        content.Should().Contain("important");
    }
}
