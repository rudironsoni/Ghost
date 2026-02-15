using Ghost.Consent;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Tests.Consent;

public class ShadowDOMHelperTests
{
    [Fact]
    public async Task FindInShadowDOMAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        IPage? page = null;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.FindInShadowDOMAsync(page, ".test").ConfigureAwait(false)).ConfigureAwait(false);
#pragma warning restore CS8604
    }

    [Fact]
    public async Task FindInShadowDOMAsync_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var mockPage = new Mock<IPage>();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.FindInShadowDOMAsync(mockPage.Object, null).ConfigureAwait(false)).ConfigureAwait(false);
#pragma warning restore CS8625
    }

    [Fact]
    public async Task ClickInShadowDOMAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        IPage? page = null;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.ClickInShadowDOMAsync(page, ".test").ConfigureAwait(false)).ConfigureAwait(false);
#pragma warning restore CS8604
    }

    [Fact]
    public async Task ClickInShadowDOMAsync_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var mockPage = new Mock<IPage>();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.ClickInShadowDOMAsync(mockPage.Object, null).ConfigureAwait(false)).ConfigureAwait(false);
#pragma warning restore CS8625
    }

    [Fact]
    public async Task GetShadowRootCountAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        IPage? page = null;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.GetShadowRootCountAsync(page).ConfigureAwait(false)).ConfigureAwait(false);
#pragma warning restore CS8604
    }

    [Fact]
    public async Task GetShadowRootCountAsync_WithError_ReturnsZero()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.EvaluateAsync<int>(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        int count = await ShadowDOMHelper.GetShadowRootCountAsync(mockPage.Object).ConfigureAwait(false);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task FindInShadowDOMAsync_WhenPiercingSelectorFails_ReturnsFalse()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);
        mockPage.Setup(p => p.EvaluateAsync<bool>(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        bool result = await ShadowDOMHelper.FindInShadowDOMAsync(mockPage.Object, ".test").ConfigureAwait(false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ClickInShadowDOMAsync_WhenPiercingSelectorFails_ReturnsFalse()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.QuerySelectorAsync(It.IsAny<string>()))
            .ReturnsAsync((IElement?)null);
        mockPage.Setup(p => p.EvaluateAsync<bool>(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        bool result = await ShadowDOMHelper.ClickInShadowDOMAsync(mockPage.Object, ".test").ConfigureAwait(false);

        // Assert
        Assert.False(result);
    }
}
