using Ghost.Consent;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Tests.Consent;

public class ConsentFlowHandlerTests
{
    [Fact]
    public void Constructor_WithNoParameters_CreatesInstance()
    {
        // Act
        var handler = new ConsentFlowHandler();

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        IPage? page = null;
        var config = new CMPConfig
        {
            Name = "test",
            Detectors = [".test"],
            AcceptButton = ".accept",
            MultiStep = true,
            Steps = [".step1", ".step2"]
        };

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await handler.ExecuteMultiStepFlowAsync(page, config).ConfigureAwait(false));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        var mockPage = new Mock<IPage>();
        CMPConfig? config = null;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await handler.ExecuteMultiStepFlowAsync(mockPage.Object, config).ConfigureAwait(false));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WithNoSteps_ReturnsFalse()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        var mockPage = new Mock<IPage>();
        var config = new CMPConfig
        {
            Name = "test",
            Detectors = [".test"],
            AcceptButton = ".accept",
            MultiStep = true,
            Steps = null
        };

        // Act
        bool result = await handler.ExecuteMultiStepFlowAsync(mockPage.Object, config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WithEmptySteps_ReturnsFalse()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        var mockPage = new Mock<IPage>();
        var config = new CMPConfig
        {
            Name = "test",
            Detectors = [".test"],
            AcceptButton = ".accept",
            MultiStep = true,
            Steps = []
        };

        // Act
        bool result = await handler.ExecuteMultiStepFlowAsync(mockPage.Object, config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DetectElementAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        IPage? page = null;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ConsentFlowHandler.DetectElementAsync(page, ".test").ConfigureAwait(false));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task DetectElementAsync_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var mockPage = new Mock<IPage>();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ConsentFlowHandler.DetectElementAsync(mockPage.Object, null).ConfigureAwait(false));
#pragma warning restore CS8625
    }

    [Fact]
    public async Task DetectElementAsync_WhenElementNotFound_ReturnsFalse()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.QuerySelectorAsync(It.IsAny<string>())).ReturnsAsync((IElement?)null);

        // Act
        bool result = await ConsentFlowHandler.DetectElementAsync(mockPage.Object, ".not-found", checkShadowDOM: false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WhenStepNotFound_ReturnsFalse()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.QuerySelectorAsync(It.IsAny<string>())).ReturnsAsync((IElement?)null);

        var config = new CMPConfig
        {
            Name = "test",
            Detectors = [".test"],
            AcceptButton = ".accept",
            MultiStep = true,
            Steps = [".step1"]
        };

        // Act
        bool result = await handler.ExecuteMultiStepFlowAsync(mockPage.Object, config);

        // Assert
        Assert.False(result);
    }
}
