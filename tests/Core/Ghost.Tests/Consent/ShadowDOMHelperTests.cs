using Ghost.Consent;
using Microsoft.Playwright;
using NSubstitute;
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
            async () => await ShadowDOMHelper.FindInShadowDOMAsync(page, ".test"));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task FindInShadowDOMAsync_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var page = Substitute.For<IPage>();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.FindInShadowDOMAsync(page, null));
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
            async () => await ShadowDOMHelper.ClickInShadowDOMAsync(page, ".test"));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task ClickInShadowDOMAsync_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var page = Substitute.For<IPage>();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.ClickInShadowDOMAsync(page, null));
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
            async () => await ShadowDOMHelper.GetShadowRootCountAsync(page));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task GetShadowRootCountAsync_WithError_ReturnsZero()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.EvaluateAsync<int>(Arg.Any<string>())
            .Returns(Task.FromException<int>(new Exception("Test error")));

        // Act
        var count = await ShadowDOMHelper.GetShadowRootCountAsync(page);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task FindInShadowDOMAsync_WhenPiercingSelectorFails_ReturnsFalse()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.QuerySelectorAsync(Arg.Any<string>())
            .Returns(Task.FromResult<IElement?>(null));
        page.EvaluateAsync<bool>(Arg.Any<string>())
            .Returns(Task.FromException<bool>(new Exception("Test error")));

        // Act
        var result = await ShadowDOMHelper.FindInShadowDOMAsync(page, ".test");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ClickInShadowDOMAsync_WhenPiercingSelectorFails_ReturnsFalse()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.QuerySelectorAsync(Arg.Any<string>())
            .Returns(Task.FromResult<IElement?>(null));
        page.EvaluateAsync<bool>(Arg.Any<string>())
            .Returns(Task.FromException<bool>(new Exception("Test error")));

        // Act
        var result = await ShadowDOMHelper.ClickInShadowDOMAsync(page, ".test");

        // Assert
        Assert.False(result);
    }
}
