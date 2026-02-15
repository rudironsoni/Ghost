using FluentAssertions;
using Ghost.Core;
using Ghost.Testing.Fakes;
using Xunit;

namespace Ghost.Core.Unit.Tests;

/// <summary>
/// Hermetic unit tests for GhostKernel functionality using StubGhostKernel.
/// These tests run without real browsers and complete in milliseconds.
/// </summary>
public class GhostKernelHermeticTests
{
    [Fact]
    public async Task StubKernel_ShouldCreateSession()
    {
        // Arrange
        var kernel = new StubGhostKernel();

        // Act
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrEmpty();
        session.IsConnected.Should().BeTrue();

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubSession_ShouldCreatePage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);

        // Act
        IPage page = await session.NewPageAsync().ConfigureAwait(false);

        // Assert
        page.Should().NotBeNull();
        page.PageId.Should().NotBeNullOrEmpty();
        page.Url.Should().Be("about:blank");

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubPage_ShouldNavigateToUrl()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);

        // Act
        await page.NavigateAsync("https://example.com").ConfigureAwait(false);

        // Assert
        page.Url.Should().Be("https://example.com");
        (await page.GetTitleAsync().ConfigureAwait(false)).Should().Contain("example.com");

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubPage_ShouldSetAndGetContent()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);
        const string testHtml = "<html><body><h1>Test Title</h1></body></html>";

        // Act
        await page.SetContentAsync(testHtml).ConfigureAwait(false);
        string content = await page.GetContentAsync().ConfigureAwait(false);

        // Assert
        content.Should().Be(testHtml);

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubPage_ShouldQuerySelector()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);

        // Act
        IElement? element = await page.QuerySelectorAsync("h1").ConfigureAwait(false);

        // Assert
        element.Should().NotBeNull();

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubPage_ShouldSupportMultiplePages()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);

        // Act
        IPage page1 = await session.NewPageAsync().ConfigureAwait(false);
        IPage page2 = await session.NewPageAsync().ConfigureAwait(false);

        await page1.NavigateAsync("https://example.com").ConfigureAwait(false);
        await page2.NavigateAsync("https://test.com").ConfigureAwait(false);

        // Assert
        page1.Url.Should().Be("https://example.com");
        page2.Url.Should().Be("https://test.com");
        session.Pages.Should().HaveCount(2);

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubKernel_ShouldSupportMultipleSessions()
    {
        // Arrange
        var kernel = new StubGhostKernel();

        // Act
        IBrowserSession session1 = await kernel.NewSessionAsync().ConfigureAwait(false);
        IBrowserSession session2 = await kernel.NewSessionAsync().ConfigureAwait(false);

        // Assert
        session1.SessionId.Should().NotBe(session2.SessionId);

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubPage_ShouldPerformClickAction()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);

        // Act - Should not throw
        Func<Task> act = async () => await page.ClickAsync("button").ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync().ConfigureAwait(false);

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubPage_ShouldPerformTypeAction()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);

        // Act - Should not throw
        Func<Task> act = async () => await page.TypeAsync("input", "test text").ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync().ConfigureAwait(false);

        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task StubPage_ShouldPerformFillAction()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync().ConfigureAwait(false);
        IPage page = await session.NewPageAsync().ConfigureAwait(false);

        // Act - Should not throw
        Func<Task> act = async () => await page.FillAsync("input", "test value").ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync().ConfigureAwait(false);

        await kernel.DisposeAsync().ConfigureAwait(false);
    }
}
