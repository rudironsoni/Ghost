using System.Linq;
using FluentAssertions;
using Xunit;

namespace Ghost.Tests.Abstractions;

public class ClickOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var o = new ClickOptions();
        o.Button.Should().Be("left");
        o.ClickCount.Should().Be(1);
        o.Delay.Should().Be(0);
        o.Modifiers.Should().BeEmpty();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var o = new ClickOptions { Button = "right", ClickCount = 2, Delay = 10, Modifiers = new[] { "Shift" } };
        o.Button.Should().Be("right");
        o.ClickCount.Should().Be(2);
        o.Delay.Should().Be(10);
        o.Modifiers.Should().Contain("Shift");
    }
}
