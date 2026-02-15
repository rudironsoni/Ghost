using FluentAssertions;
using Ghost.Testing.Fakes;
using Xunit;

namespace Ghost.Core.Unit.Tests;

/// <summary>
/// Hermetic unit tests for FakeElement functionality.
/// Tests element interaction without real browser dependencies.
/// </summary>
public class FakeElementHermeticTests
{
    [Fact]
    public async Task FakeElement_ShouldReturnEmptyTextContentByDefault()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        string? text = await element.GetTextContentAsync().ConfigureAwait(false);

        // Assert
        text.Should().BeEmpty();
    }

    [Fact]
    public async Task FakeElement_ShouldStoreAndRetrieveTextContent()
    {
        // Arrange
        var element = new FakeElement();
        element.SetTextContent("Software Engineer");

        // Act
        string? text = await element.GetTextContentAsync().ConfigureAwait(false);

        // Assert
        text.Should().Be("Software Engineer");
    }

    [Fact]
    public async Task FakeElement_ShouldStoreAndRetrieveAttribute()
    {
        // Arrange
        var element = new FakeElement();
        element.SetAttribute("href", "https://example.com/job/123");

        // Act
        string? href = await element.GetAttributeAsync("href").ConfigureAwait(false);

        // Assert
        href.Should().Be("https://example.com/job/123");
    }

    [Fact]
    public async Task FakeElement_ShouldReturnNullForMissingAttribute()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        string? attr = await element.GetAttributeAsync("missing").ConfigureAwait(false);

        // Assert
        attr.Should().BeNull();
    }

    [Fact]
    public async Task FakeElement_ShouldStoreAndRetrieveInnerHtml()
    {
        // Arrange
        var element = new FakeElement();
        element.SetInnerHtml("<span>Test</span>");

        // Act
        string? html = await element.GetInnerHtmlAsync().ConfigureAwait(false);

        // Assert
        html.Should().Be("<span>Test</span>");
    }

    [Fact]
    public async Task FakeElement_ShouldReportVisible()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        bool isVisible = await element.IsVisibleAsync().ConfigureAwait(false);

        // Assert
        isVisible.Should().BeTrue();
    }

    [Fact]
    public async Task FakeElement_ShouldReportEnabled()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        bool isEnabled = await element.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task FakeElement_ShouldReportNotCheckedByDefault()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        bool isChecked = await element.IsCheckedAsync().ConfigureAwait(false);

        // Assert
        isChecked.Should().BeFalse();
    }

    [Fact]
    public async Task FakeElement_ShouldSupportClickAction()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        Func<Task> act = async () => await element.ClickAsync().ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task FakeElement_ShouldSupportTypeAction()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        Func<Task> act = async () => await element.TypeAsync("test input").ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task FakeElement_ShouldSupportFillAction()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        Func<Task> act = async () => await element.FillAsync("test value").ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task FakeElement_ShouldSupportMultipleAttributes()
    {
        // Arrange
        var element = new FakeElement();
        element.SetAttribute("id", "job-123");
        element.SetAttribute("class", "job-card");
        element.SetAttribute("data-location", "Remote");

        // Act
        string? id = await element.GetAttributeAsync("id").ConfigureAwait(false);
        string? cssClass = await element.GetAttributeAsync("class").ConfigureAwait(false);
        string? location = await element.GetAttributeAsync("data-location").ConfigureAwait(false);

        // Assert
        id.Should().Be("job-123");
        cssClass.Should().Be("job-card");
        location.Should().Be("Remote");
    }

    [Fact]
    public async Task FakeElement_ShouldQueryNestedElements()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        IElement? nested = await element.QuerySelectorAsync(".nested").ConfigureAwait(false);

        // Assert
        nested.Should().NotBeNull();
    }

    [Fact]
    public async Task FakeElement_ShouldDisposeWithoutErrors()
    {
        // Arrange
        var element = new FakeElement();

        // Act
        Func<Task> act = async () => await element.DisposeAsync().ConfigureAwait(false);

        // Assert
        await act.Should().NotThrowAsync().ConfigureAwait(false);
    }
}
