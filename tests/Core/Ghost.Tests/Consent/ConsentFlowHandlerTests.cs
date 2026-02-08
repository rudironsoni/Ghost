using Ghost.Consent;
using Microsoft.Playwright;
using NSubstitute;
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
            async () => await handler.ExecuteMultiStepFlowAsync(page, config));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        var page = Substitute.For<IPage>();
        CMPConfig? config = null;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await handler.ExecuteMultiStepFlowAsync(page, config));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WithNoSteps_ReturnsFalse()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        var page = Substitute.For<IPage>();
        var config = new CMPConfig
        {
            Name = "test",
            Detectors = [".test"],
            AcceptButton = ".accept",
            MultiStep = true,
            Steps = null
        };

        // Act
        var result = await handler.ExecuteMultiStepFlowAsync(page, config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WithEmptySteps_ReturnsFalse()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        var page = Substitute.For<IPage>();
        var config = new CMPConfig
        {
            Name = "test",
            Detectors = [".test"],
            AcceptButton = ".accept",
            MultiStep = true,
            Steps = []
        };

        // Act
        var result = await handler.ExecuteMultiStepFlowAsync(page, config);

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
            async () => await ConsentFlowHandler.DetectElementAsync(page, ".test"));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task DetectElementAsync_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var page = Substitute.For<IPage>();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ConsentFlowHandler.DetectElementAsync(page, null));
#pragma warning restore CS8625
    }

    [Fact]
    public async Task DetectElementAsync_WhenElementNotFound_ReturnsFalse()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.QuerySelectorAsync(Arg.Any<string>()).Returns(Task.FromResult<IElement?>(null));

        // Act
        var result = await ConsentFlowHandler.DetectElementAsync(page, ".not-found", checkShadowDOM: false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteMultiStepFlowAsync_WhenStepNotFound_ReturnsFalse()
    {
        // Arrange
        var handler = new ConsentFlowHandler();
        var page = Substitute.For<IPage>();
        page.QuerySelectorAsync(Arg.Any<string>()).Returns(Task.FromResult<IElement?>(null));

        var config = new CMPConfig
        {
            Name = "test",
            Detectors = [".test"],
            AcceptButton = ".accept",
            MultiStep = true,
            Steps = [".step1"]
        };

        // Act
        var result = await handler.ExecuteMultiStepFlowAsync(page, config);

        // Assert
        Assert.False(result);
    }
}
