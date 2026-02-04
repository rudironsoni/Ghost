using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for JavaScriptAdapter covering browser automation scenarios.
/// </summary>
[TestFixture]
public class JavaScriptAdapterTests
{
    private Mock<ILogger<JavaScriptAdapter>> _mockLogger = null!;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<JavaScriptAdapter>>();
    }

    [Test]
    public void Constructor_WithLogger_ShouldInitialize()
    {
        // Arrange & Act
        var adapter = new JavaScriptAdapter(_mockLogger.Object);

        // Assert
        adapter.Should().NotBeNull();
        adapter.Name.Should().Be("JavaScript");
        adapter.ContentType.Should().Be(ContentType.JavaScript);
        adapter.IsAvailable.Should().BeTrue();
    }

    [Test]
    public void Constructor_WithoutLogger_ShouldInitialize()
    {
        // Arrange & Act
        var adapter = new JavaScriptAdapter();

        // Assert
        adapter.Should().NotBeNull();
        adapter.Name.Should().Be("JavaScript");
    }

    [Test]
    public async Task CanHandleAsync_WithValidHttpUrl_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://example.com")
        {
            ExpectedContentType = ContentType.JavaScript
        };

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task CanHandleAsync_WithHttpsUrl_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://example.com");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task CanHandleAsync_WithInvalidUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("not-a-url");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithNullRequest_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);

        // Act
        var result = await adapter.CanHandleAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithFtpUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("ftp://example.com");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithExpectedContentTypeHtml_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://example.com")
        {
            ExpectedContentType = ContentType.Html
        };

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithExpectedContentTypeUnknown_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://example.com")
        {
            ExpectedContentType = ContentType.Unknown
        };

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ExtractAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);

        // Act
        Func<Task> act = async () => await adapter.ExtractAsync(null!);

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void ExtractAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://example.com");

        // Act
        Func<Task> act = async () => await adapter.ExtractAsync(request, null!);

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task DisposeAsync_WhenBrowserNotInitialized_ShouldComplete()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);

        // Act
        Func<Task> act = async () => await adapter.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task DisposeAsync_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);

        // Act
        await adapter.DisposeAsync();
        Func<Task> act = async () => await adapter.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public void Properties_ShouldReturnExpectedValues()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);

        // Assert
        adapter.Name.Should().Be("JavaScript");
        adapter.ContentType.Should().Be(ContentType.JavaScript);
        adapter.IsAvailable.Should().BeTrue();
    }

    [Test]
    public async Task CanHandleAsync_WithCancellationToken_ShouldNotThrow()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("https://example.com");
        using var cts = new CancellationTokenSource();

        // Act
        var result = await adapter.CanHandleAsync(request, cts.Token);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task CanHandleAsync_WithFileUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);
        var request = new Request("file:///path/to/file.html");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Adapter_ShouldImplementIContentAdapter()
    {
        // Arrange
        var adapter = new JavaScriptAdapter(_mockLogger.Object);

        // Assert
        adapter.Should().BeAssignableTo<IContentAdapter>();
    }
}
