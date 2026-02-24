using Ghost.Consent;
using Microsoft.Playwright;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Consent;

public class ShadowDOMHelperTests : ReliabilityTestBase
{
    public ShadowDOMHelperTests(ITestOutputHelper output) : base(output) { }

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
        var mockPage = new Mock<IPage>();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.FindInShadowDOMAsync(mockPage.Object, null));
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
        var mockPage = new Mock<IPage>();

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ShadowDOMHelper.ClickInShadowDOMAsync(mockPage.Object, null));
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
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.EvaluateAsync<int>(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        int count = await ShadowDOMHelper.GetShadowRootCountAsync(mockPage.Object);

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
        bool result = await ShadowDOMHelper.FindInShadowDOMAsync(mockPage.Object, ".test");

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
        bool result = await ShadowDOMHelper.ClickInShadowDOMAsync(mockPage.Object, ".test");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("';alert('xss');//")]
    [InlineData("div[onclick='alert(1)')]")]
    [InlineData("eval(alert('xss'))")]
    [InlineData("//comment")]
    [InlineData("/*comment*/")]
    [InlineData("@import url()")]
    [InlineData("div; alert('xss')")]
    public async Task FindInShadowDOMAsync_WithXssAttempt_ThrowsArgumentException(string maliciousSelector)
    {
        // Arrange
        var mockPage = new Mock<IPage>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await ShadowDOMHelper.FindInShadowDOMAsync(mockPage.Object, maliciousSelector));
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("';alert('xss');//")]
    [InlineData("div[onerror='alert(1)')]")]
    [InlineData("${alert('xss')}")]
    public async Task ClickInShadowDOMAsync_WithXssAttempt_ThrowsArgumentException(string maliciousSelector)
    {
        // Arrange
        var mockPage = new Mock<IPage>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await ShadowDOMHelper.ClickInShadowDOMAsync(mockPage.Object, maliciousSelector));
    }

    [Theory]
    [InlineData(".my-class")]
    [InlineData("#my-id")]
    [InlineData("div[data-test='value']")]
    [InlineData("button.btn-primary")]
    [InlineData("input[type='text']")]
    [InlineData("div > span")]
    [InlineData(".item:nth-child(2)")]
    [InlineData("[data-test^='prefix']")]
    public async Task FindInShadowDOMAsync_WithValidSelector_CallsQuerySelector(string validSelector)
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.QuerySelectorAsync($"pierce/{validSelector}"))
            .ReturnsAsync((IElement?)null);
        mockPage.Setup(p => p.EvaluateAsync<bool>(It.IsAny<string>(), It.IsAny<object?>()))
            .ReturnsAsync(false);

        // Act
        await ShadowDOMHelper.FindInShadowDOMAsync(mockPage.Object, validSelector);

        // Assert - should call QuerySelectorAsync with pierce selector
        mockPage.Verify(p => p.QuerySelectorAsync($"pierce/{validSelector}"), Times.Once);
    }
}
