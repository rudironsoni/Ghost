using FluentAssertions;
using Xunit;

namespace Ghost.Tests.Abstractions;

public class TypeOptionsTests
{
    [Fact]
    public void DefaultsAreExpected()
    {
        var o = new TypeOptions();
        o.Delay.Should().Be(0);
    }

    [Fact]
    public void DelayCanBeSet()
    {
        var o = new TypeOptions { Delay = 50 };
        o.Delay.Should().Be(50);
    }
}
