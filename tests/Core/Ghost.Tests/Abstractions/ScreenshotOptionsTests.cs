using FluentAssertions;
using Xunit;

namespace Ghost.Tests.Abstractions;

public class ScreenshotOptionsTests
{
    [Fact]
    public void DefaultsAreExpected()
    {
        var o = new ScreenshotOptions();
        o.Path.Should().BeNull();
        o.Type.Should().Be("png");
        o.Quality.Should().BeNull();
        o.FullPage.Should().BeFalse();
    }

    [Fact]
    public void PropertiesCanBeSet()
    {
        var o = new ScreenshotOptions { Path = "p", Type = "jpeg", Quality = 50, FullPage = true };
        o.Path.Should().Be("p");
        o.Type.Should().Be("jpeg");
        o.Quality.Should().Be(50);
        o.FullPage.Should().BeTrue();
    }
}
