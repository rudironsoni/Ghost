using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Unit tests for JavaScriptAdapter with mocked Playwright dependencies.
/// </summary>
[TestFixture]
public class JavaScriptAdapterMockTests
{
    [Test]
    public void Name_ShouldReturnJavaScript()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();

        // Act & Assert
        adapter.Name.Should().Be("JavaScript");
    }

    [Test]
    public void ContentType_ShouldReturnJavaScript()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();

        // Act & Assert
        adapter.ContentType.Should().Be(ContentType.JavaScript);
    }

    [Test]
    public void IsAvailable_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();

        // Act & Assert
        adapter.IsAvailable.Should().BeTrue();
    }

    [Test]
    public async Task CanHandleAsync_WithHttpUrl_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();
        var request = new Request("https://example.com");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task CanHandleAsync_WithNullRequest_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();

        // Act
        var result = await adapter.CanHandleAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithInvalidUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();
        var request = new Request("not-a-valid-url");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithFtpScheme_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();
        var request = new Request("ftp://example.com");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleAsync_WithJavaScriptContentType_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();
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
    public async Task CanHandleAsync_WithNonJavaScriptContentType_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();
        var request = new Request("https://example.com")
        {
            ExpectedContentType = ContentType.StaticHtml
        };

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task ExtractAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();

        // Act & Assert
        await adapter.Invoking(a => a.ExtractAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task ExtractAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();
        var request = new Request("https://example.com");

        // Act & Assert
        await adapter.Invoking(a => a.ExtractAsync(request, null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var adapter = new JavaScriptAdapter();

        // Act & Assert
        await adapter.Invoking(a => a.DisposeAsync().AsTask())
            .Should().NotThrowAsync();
        
        await adapter.Invoking(a => a.DisposeAsync().AsTask())
            .Should().NotThrowAsync();
    }

    [Test]
    public void Constructor_WithLogger_ShouldNotThrow()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<JavaScriptAdapter>>();

        // Act & Assert
        var adapter = new JavaScriptAdapter(mockLogger.Object);
        adapter.Should().NotBeNull();
    }

    [Test]
    public void Constructor_WithoutLogger_ShouldNotThrow()
    {
        // Act & Assert
        var adapter = new JavaScriptAdapter();
        adapter.Should().NotBeNull();
    }
}
