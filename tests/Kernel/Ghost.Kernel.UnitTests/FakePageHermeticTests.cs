using FluentAssertions;
using Ghost.Testing.Fakes;
using Xunit;

namespace Ghost.Kernel.Unit.Tests;

/// <summary>
/// Hermetic unit tests for FakePage functionality.
/// Tests the fake page's ability to store and retrieve content without real browsers.
/// </summary>
public class FakePageHermeticTests
{
    [Fact]
    public async Task FakePage_ShouldHaveDefaultContent()
    {
        // Arrange
        var page = new FakePage();

        // Act
        string content = await page.GetContentAsync();

        // Assert
        content.Should().Be("<html><body></body></html>");
    }

    [Fact]
    public async Task FakePage_ShouldStoreAndRetrieveContent()
    {
        // Arrange
        var page = new FakePage();
        const string html = "<html><body><h1>Test</h1></body></html>";

        // Act
        await page.SetContentAsync(html);
        string content = await page.GetContentAsync();

        // Assert
        content.Should().Be(html);
    }

    [Fact]
    public async Task FakePage_ShouldNavigateToUrl()
    {
        // Arrange
        var page = new FakePage();

        // Act
        await page.NavigateAsync("https://example.com");

        // Assert
        page.Url.Should().Be("https://example.com");
        (await page.GetTitleAsync()).Should().Be("Page: https://example.com");
    }

    [Fact]
    public void FakePage_ShouldHaveUniquePageId()
    {
        // Arrange & Act
        var page1 = new FakePage();
        var page2 = new FakePage();

        // Assert
        page1.PageId.Should().NotBe(page2.PageId);
    }

    [Fact]
    public async Task FakePage_ShouldReturnRegisteredElement()
    {
        // Arrange
        var page = new FakePage();
        var element = new FakeElement();
        element.SetTextContent("Job Title");
        page.RegisterElement("h1", element);

        // Act
        IElement? result = await page.QuerySelectorAsync("h1");

        // Assert
        result.Should().NotBeNull();
        string? text = await result!.GetTextContentAsync();
        text.Should().Be("Job Title");
    }

    [Fact]
    public async Task FakePage_ShouldReturnDefaultElementWhenNotRegistered()
    {
        // Arrange
        var page = new FakePage();

        // Act
        IElement? result = await page.QuerySelectorAsync("div");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FakePage_ShouldSupportWaitForSelector()
    {
        // Arrange
        var page = new FakePage();

        // Act
        IElement element = await page.WaitForSelectorAsync(".job-card");

        // Assert
        element.Should().NotBeNull();
    }

    [Fact]
    public async Task FakePage_ShouldSupportScreenshot()
    {
        // Arrange
        var page = new FakePage();

        // Act
        byte[] screenshot = await page.ScreenshotAsync();

        // Assert
        screenshot.Should().NotBeNull();
        screenshot.Should().BeEmpty(); // Fake returns empty array
    }

    [Fact]
    public async Task FakePage_ShouldSupportPdfGeneration()
    {
        // Arrange
        var page = new FakePage();

        // Act
        byte[] pdf = await page.PdfAsync();

        // Assert
        pdf.Should().NotBeNull();
        pdf.Should().BeEmpty(); // Fake returns empty array
    }

    [Fact]
    public async Task FakePage_ShouldDisposeWithoutErrors()
    {
        // Arrange
        var page = new FakePage();

        // Act
        Func<Task> act = async () => await page.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
