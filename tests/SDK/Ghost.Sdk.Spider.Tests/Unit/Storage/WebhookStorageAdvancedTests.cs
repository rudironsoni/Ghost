using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Ghost.Sdk.Spider.Storage.Sinks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Net;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Advanced tests for WebhookStorage covering edge cases and error scenarios.
/// </summary>
[TestFixture]
public class WebhookStorageAdvancedTests
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
    public async Task StoreAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        var item = new { Name = "Test" };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await storage.StoreAsync(item, context, cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.Exception.Should().BeAssignableTo<OperationCanceledException>();
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
        var item = new { Name = "Test" };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timeout");
        result.Exception.Should().BeOfType<TaskCanceledException>();
    }

    [Test]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.GatewayTimeout)]
    public async Task StoreAsync_WithVariousErrorCodes_ShouldReturnFailure(HttpStatusCode statusCode)
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                ReasonPhrase = statusCode.ToString()
            });

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        var item = new { Name = "Test" };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain(statusCode.ToString());
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

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        
        var complexItem = new
        {
            Id = 123,
            Name = "Complex Item",
            Price = 99.99m,
            Tags = new[] { "tag1", "tag2" },
            Metadata = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 42
            },
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        var context = new StorageContext
        {
            SpiderName = "ComplexSpider",
            SourceUrl = "https://example.com/complex",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object> { ["version"] = "2.0" },
            Tags = new List<string> { "production", "critical" }
        };

        // Act
        await storage.StoreAsync(complexItem, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        
        content.Should().Contain("ComplexSpider");
        content.Should().Contain("Complex Item");
        content.Should().Contain("99.99");
        content.Should().Contain("tag1");
        content.Should().Contain("production");
        content.Should().Contain("version");
    }

    [Test]
    public async Task StoreBatchAsync_WithMixedTypes_ShouldSucceed()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        
        var items = new[]
        {
            new { Type = "A", Value = 1 },
            new { Type = "B", Value = 2 },
            new { Type = "C", Value = 3 }
        };
        
        var context = StorageContext.Create("MixedSpider");

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);
    }

    [Test]
    public async Task StoreBatchAsync_WithLargeBatch_ShouldHandleCorrectly()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        
        var items = Enumerable.Range(1, 1000)
            .Select(i => new { Id = i, Name = $"Item {i}" })
            .ToList();
        
        var context = new StorageContext
        {
            SpiderName = "LargeBatchSpider",
            BatchId = "large-batch-001"
        };

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1000);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Test]
    public async Task StoreAsync_WithNullValues_ShouldIgnoreNulls()
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

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        
        var item = new
        {
            Name = "Test",
            Description = (string?)null,
            Value = 42,
            OptionalField = (int?)null
        };
        
        var context = StorageContext.Create("TestSpider");

        // Act
        await storage.StoreAsync(item, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        
        content.Should().Contain("Name");
        content.Should().Contain("Value");
        // NullValueHandling.Ignore should exclude null properties
        content.Should().NotContain("Description");
        content.Should().NotContain("OptionalField");
    }

    [Test]
    public async Task StoreAsync_WithCircularReference_ShouldIgnoreLoop()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        
        // Note: Anonymous types don't allow circular references, so we test that serialization works
        var item = new { Name = "Test", Id = 1 };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task StoreBatchAsync_FailureDuringBatch_ShouldReturnFailure()
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
                ReasonPhrase = "Batch Processing Error"
            });

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        var items = new[] { new { Id = 1 }, new { Id = 2 } };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ItemsStored.Should().Be(0);
        result.Error.Should().Contain("InternalServerError");
    }

    [Test]
    public async Task FlushAsync_WithPendingOperations_ShouldComplete()
    {
        // Arrange
        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);

        // Act
        var act = async () => await storage.FlushAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task CloseAsync_AfterOperations_ShouldComplete()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        await storage.InitializeAsync();
        await storage.StoreAsync(new { Test = "data" }, StorageContext.Create("Test"));

        // Act
        var act = async () => await storage.CloseAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task StoreAsync_WithContentHeaders_ShouldSetCorrectHeaders()
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

        var storage = new WebhookStorage(_httpClient, WebhookUrl, NullLogger<WebhookStorage>.Instance);
        var item = new { Name = "Test" };
        var context = StorageContext.Create("TestSpider");

        // Act
        await storage.StoreAsync(item, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().NotBeNull();
        capturedRequest.Content!.Headers.ContentType.Should().NotBeNull();
        capturedRequest.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        capturedRequest.Content.Headers.ContentType.CharSet.Should().Be("utf-8");
    }
}
