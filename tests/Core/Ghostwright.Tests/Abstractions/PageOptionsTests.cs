using FluentAssertions;
using Xunit;

namespace Ghostwright.Tests.Abstractions;

public class PageOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var o = new PageOptions();
        o.Width.Should().Be(1280);
        o.Height.Should().Be(720);
        o.UserAgent.Should().BeNull();
        o.JavaScriptEnabled.Should().BeTrue();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var o = new PageOptions { Width = 300, Height = 400, UserAgent = "x", JavaScriptEnabled = false };
        o.Width.Should().Be(300);
        o.Height.Should().Be(400);
        o.UserAgent.Should().Be("x");
        o.JavaScriptEnabled.Should().BeFalse();
    }
}
