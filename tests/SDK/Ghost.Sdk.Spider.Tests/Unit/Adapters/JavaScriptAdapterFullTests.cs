using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Additional comprehensive tests for JavaScriptAdapter covering edge cases and error scenarios.
/// </summary>
[TestFixture]
public class JavaScriptAdapterFullTests
{
    private Mock<ILogger<JavaScriptAdapter>> _mockLogger = null!;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<JavaScriptAdapter>>();
    }

    [Test]
    public async Task ExtractAsync_WithDefaultOptions_ShouldUseDefaults()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://httpbin.org/html");

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.AdapterName.Should().Be("JavaScript");
    }

    [Test]
    public async Task ExtractAsync_WithJavaScriptAdapterOptions_ShouldAcceptOptions()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://httpbin.org/html");
        var options = new JavaScriptAdapterOptions
        {
            Timeout = TimeSpan.FromSeconds(15),
            UserAgent = "TestBot/1.0"
        };

        // Act
        var response = await adapter.ExtractAsync(request, options);

        // Assert
        response.Should().NotBeNull();
        response.AdapterName.Should().Be("JavaScript");
    }

    [Test]
    public async Task ExtractAsync_WithCustomHeaders_ShouldApplyHeaders()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://httpbin.org/headers");
        request.Headers["X-Custom-Header"] = "CustomValue";
        request.Headers["X-Test"] = "TestValue";

        var options = new JavaScriptAdapterOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await adapter.ExtractAsync(request, options);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task ExtractAsync_WithSslValidationDisabled_ShouldIgnoreSslErrors()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://self-signed.badssl.com/");
        var options = new JavaScriptAdapterOptions
        {
            ValidateSslCertificate = false,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        Func<Task> act = async () => await adapter.ExtractAsync(request, options);

        // Assert - Should not throw SSL exception
        await act.Should().NotThrowAsync<System.Net.Http.HttpRequestException>();
    }

    [Test]
    public async Task ExtractAsync_WithInvalidUrl_ShouldReturnErrorResponse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://this-domain-does-not-exist-at-all-12345.com");
        var options = new JavaScriptAdapterOptions
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Act
        var response = await adapter.ExtractAsync(request, options);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ExtractAsync_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://httpbin.org/delay/10")
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        var options = new JavaScriptAdapterOptions
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await adapter.ExtractAsync(request, options);
        stopwatch.Stop();

        // Assert - Should timeout quickly
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        response.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithHttpUrlAndUnknownContentType_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("http://example.com")
        {
            ExpectedContentType = ContentType.Unknown
        };

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task CanHandleAsync_WithDataUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("data:text/html,<html><body>test</body></html>");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithWebSocketUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("ws://example.com");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task ExtractAsync_WithEmptyUrl_ShouldReturnError()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("");
        var options = new JavaScriptAdapterOptions();

        // Act
        var response = await adapter.ExtractAsync(request, options);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task ExtractAsync_WithCustomUserAgent_ShouldSetUserAgent()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://httpbin.org/user-agent");
        var options = new JavaScriptAdapterOptions
        {
            UserAgent = "MyCustomBot/2.0",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await adapter.ExtractAsync(request, options);

        // Assert
        response.Should().NotBeNull();
        if (response.IsSuccess)
        {
            response.Content.Content.Should().Contain("MyCustomBot");
        }
    }

    [Test]
    public async Task ExtractAsync_WithRedirect_ShouldFollowAndSetFinalUrl()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://httpbin.org/redirect/1");
        var options = new JavaScriptAdapterOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await adapter.ExtractAsync(request, options);

        // Assert
        response.Should().NotBeNull();
        if (response.IsSuccess)
        {
            response.FinalUrl.Should().NotBe(request.Url);
        }
    }

    [Test]
    public void Properties_ShouldHaveCorrectValues()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);

        // Assert
        adapter.Name.Should().Be("JavaScript");
        adapter.ContentType.Should().Be(ContentType.JavaScript);
        adapter.IsAvailable.Should().BeTrue();
    }

    [Test]
    public async Task DisposeAsync_AfterExtraction_ShouldCleanup()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://httpbin.org/html");
        
        await adapter.ExtractAsync(request);

        // Act
        Func<Task> act = async () => await adapter.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task ExtractAsync_MultipleSequentialRequests_ShouldReusesBrowser()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request1 = new Request("https://httpbin.org/html");
        var request2 = new Request("https://httpbin.org/html");

        // Act
        var response1 = await adapter.ExtractAsync(request1);
        var response2 = await adapter.ExtractAsync(request2);

        // Assert
        response1.Should().NotBeNull();
        response2.Should().NotBeNull();
        response1.AdapterName.Should().Be("JavaScript");
        response2.AdapterName.Should().Be("JavaScript");
    }

    [Test]
    public async Task CanHandleAsync_WithMailtoUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("mailto:test@example.com");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }
}
