using Ghost.Consent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using NSubstitute;
using Xunit;

namespace Ghost.Tests.Consent;

public class ConsentHandlerTests
{
    [Fact]
    public void Constructor_WithoutLogger_CreatesInstance()
    {
        // Act
        var handler = new ConsentHandler();

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithLogger_CreatesInstance()
    {
        // Arrange
        var logger = NullLogger<ConsentHandler>.Instance;

        // Act
        var handler = new ConsentHandler(logger);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithCustomTimeout_CreatesInstance()
    {
        // Act
        var handler = new ConsentHandler(null, 10000);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task DetectCMPAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ConsentHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.DetectCMPAsync(null!));
    }

    [Fact]
    public async Task AcceptConsentAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ConsentHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.AcceptConsentAsync(null!, "onetrust"));
    }

    [Fact]
    public async Task AcceptConsentAsync_WithNullCmpType_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ConsentHandler();
        var page = Substitute.For<IPage>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.AcceptConsentAsync(page, null!));
    }

    [Fact]
    public async Task HandleConsentAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ConsentHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.HandleConsentAsync(null!));
    }

    [Fact]
    public async Task DetectCMPAsync_WhenNoCMPPresent_ReturnsNull()
    {
        // Arrange
        var handler = new ConsentHandler();
        var page = Substitute.For<IPage>();
        page.Url.Returns("https://example.com");
        page.QuerySelectorAsync(Arg.Any<string>()).Returns(Task.FromResult<IElement?>(null));

        // Act
        var result = await handler.DetectCMPAsync(page);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AcceptConsentAsync_WithUnknownCmpType_ReturnsFalse()
    {
        // Arrange
        var handler = new ConsentHandler();
        var page = Substitute.For<IPage>();

        // Act
        var result = await handler.AcceptConsentAsync(page, "unknown-cmp");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HandleConsentAsync_WhenNoCMPDetected_ReturnsFalse()
    {
        // Arrange
        var handler = new ConsentHandler();
        var page = Substitute.For<IPage>();
        page.Url.Returns("https://example.com");
        page.QuerySelectorAsync(Arg.Any<string>()).Returns(Task.FromResult<IElement?>(null));

        // Act
        var result = await handler.HandleConsentAsync(page);

        // Assert
        Assert.False(result);
    }
}
