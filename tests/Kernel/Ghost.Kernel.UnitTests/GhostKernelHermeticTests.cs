using FluentAssertions;
using Ghost.Kernel;
using Ghost.Testing.Fakes;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Kernel.Unit.Tests;

/// <summary>
/// Hermetic unit tests for GhostKernel functionality using StubGhostKernel.
/// These tests run without real browsers and complete in milliseconds.
/// </summary>
public class GhostKernelHermeticTests : ReliabilityTestBase
{
    public GhostKernelHermeticTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task StubKernel_ShouldCreateSession()
    {
        // Arrange
        var kernel = new StubGhostKernel();

        // Act
        IBrowserSession session = await kernel.NewSessionAsync();

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrEmpty();
        session.IsConnected.Should().BeTrue();

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubSession_ShouldCreatePage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();

        // Act
        IPage page = await session.NewPageAsync();

        // Assert
        page.Should().NotBeNull();
        page.PageId.Should().NotBeNullOrEmpty();
        page.Url.Should().Be("about:blank");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubPage_ShouldNavigateToUrl()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        // Act
        await page.NavigateAsync("https://example.com");

        // Assert
        page.Url.Should().Be("https://example.com");
        (await page.GetTitleAsync()).Should().Contain("example.com");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubPage_ShouldSetAndGetContent()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();
        const string testHtml = "<html><body><h1>Test Title</h1></body></html>";

        // Act
        await page.SetContentAsync(testHtml);
        string content = await page.GetContentAsync();

        // Assert
        content.Should().Be(testHtml);

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubPage_ShouldQuerySelector()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        // Act
        IElement? element = await page.QuerySelectorAsync("h1");

        // Assert
        element.Should().NotBeNull();

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubPage_ShouldSupportMultiplePages()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();

        // Act
        IPage page1 = await session.NewPageAsync();
        IPage page2 = await session.NewPageAsync();

        await page1.NavigateAsync("https://example.com");
        await page2.NavigateAsync("https://test.com");

        // Assert
        page1.Url.Should().Be("https://example.com");
        page2.Url.Should().Be("https://test.com");
        session.Pages.Should().HaveCount(2);

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubKernel_ShouldSupportMultipleSessions()
    {
        // Arrange
        var kernel = new StubGhostKernel();

        // Act
        IBrowserSession session1 = await kernel.NewSessionAsync();
        IBrowserSession session2 = await kernel.NewSessionAsync();

        // Assert
        session1.SessionId.Should().NotBe(session2.SessionId);

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubPage_ShouldPerformClickAction()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        // Act - Should not throw
        Func<Task> act = async () => await page.ClickAsync("button");

        // Assert
        await act.Should().NotThrowAsync();

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubPage_ShouldPerformTypeAction()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        // Act - Should not throw
        Func<Task> act = async () => await page.TypeAsync("input", "test text");

        // Assert
        await act.Should().NotThrowAsync();

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StubPage_ShouldPerformFillAction()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        // Act - Should not throw
        Func<Task> act = async () => await page.FillAsync("input", "test value");

        // Assert
        await act.Should().NotThrowAsync();

        await kernel.DisposeAsync();
    }
}
