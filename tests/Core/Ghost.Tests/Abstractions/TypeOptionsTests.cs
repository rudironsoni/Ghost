using FluentAssertions;
using Xunit;

namespace Ghost.Tests.Abstractions;

public class TypeOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var o = new TypeOptions();
        o.Delay.Should().Be(0);
    }

    [Fact]
    public void Delay_CanBeSet()
    {
        var o = new TypeOptions { Delay = 50 };
        o.Delay.Should().Be(50);
    }
}
