using FluentAssertions;
using Ghost.Testing.Fakes;
using Xunit;

namespace Ghost.Kernel.Unit.Tests;

/// <summary>
/// Hermetic unit tests for browser session management.
/// Tests session lifecycle without real browser processes.
/// </summary>
public class BrowserSessionHermeticTests
{
    [Fact]
    public async Task FakeBrowserSession_ShouldHaveUniqueSessionId()
    {
        // Arrange & Act
        var session1 = new FakeBrowserSession();
        var session2 = new FakeBrowserSession();

        // Assert
        session1.SessionId.Should().NotBe(session2.SessionId);
    }

    [Fact]
    public void FakeBrowserSession_ShouldReportConnected()
    {
        // Arrange
        var session = new FakeBrowserSession();

        // Assert
        session.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task FakeBrowserSession_ShouldCreateMultiplePages()
    {
        // Arrange
        var session = new FakeBrowserSession();

        // Act
        IPage page1 = await session.NewPageAsync();
        IPage page2 = await session.NewPageAsync();
        IPage page3 = await session.NewPageAsync();

        // Assert
        session.Pages.Should().HaveCount(3);
        session.Pages.Should().Contain(page1);
        session.Pages.Should().Contain(page2);
        session.Pages.Should().Contain(page3);
    }

    [Fact]
    public async Task FakeBrowserSession_ShouldRetrievePageById()
    {
        // Arrange
        var session = new FakeBrowserSession();
        IPage page = await session.NewPageAsync();

        // Act
        IPage? retrieved = await session.GetPageAsync(page.PageId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.PageId.Should().Be(page.PageId);
    }

    [Fact]
    public async Task FakeBrowserSession_ShouldReturnNullForNonExistentPage()
    {
        // Arrange
        var session = new FakeBrowserSession();

        // Act
        IPage? page = await session.GetPageAsync("non-existent-id");

        // Assert
        page.Should().BeNull();
    }

    [Fact]
    public async Task FakeBrowserSession_ShouldClearPagesOnClose()
    {
        // Arrange
        var session = new FakeBrowserSession();
        await session.NewPageAsync();
        await session.NewPageAsync();

        // Act
        await session.CloseAsync();

        // Assert
        session.Pages.Should().BeEmpty();
    }

    [Fact]
    public async Task FakeBrowserSession_ShouldClearPagesOnDispose()
    {
        // Arrange
        var session = new FakeBrowserSession();
        await session.NewPageAsync();
        await session.NewPageAsync();

        // Act
        await session.DisposeAsync();

        // Assert
        session.Pages.Should().BeEmpty();
    }

    [Fact]
    public async Task FakeBrowserSession_ShouldSupportStorageStateSave()
    {
        // Arrange
        var session = new FakeBrowserSession();

        // Act - Should not throw
        Func<Task> act = async () => await session.SaveStorageStateAsync("/tmp/state.json").ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MultipleSessions_ShouldBeIndependent()
    {
        // Arrange
        var session1 = new FakeBrowserSession();
        var session2 = new FakeBrowserSession();

        // Act
        IPage page1 = await session1.NewPageAsync();
        IPage page2 = await session2.NewPageAsync();

        await page1.NavigateAsync("https://example.com");
        await page2.NavigateAsync("https://test.com");

        // Assert
        session1.Pages.Should().HaveCount(1);
        session2.Pages.Should().HaveCount(1);
        page1.Url.Should().Be("https://example.com");
        page2.Url.Should().Be("https://test.com");
    }

    [Fact]
    public async Task FakeBrowserSession_ShouldTrackAllCreatedPages()
    {
        // Arrange
        var session = new FakeBrowserSession();

        // Act
        var pages = new List<IPage>();
        for (int i = 0; i < 5; i++)
        {
            pages.Add(await session.NewPageAsync());
        }

        // Assert
        session.Pages.Should().HaveCount(5);
        foreach (IPage page in pages)
        {
            session.Pages.Should().Contain(page);
        }
    }
}
