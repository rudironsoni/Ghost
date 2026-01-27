using FluentAssertions;
using Xunit;

namespace Ghostwright.Tests.Abstractions;

public class WaitOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var o = new WaitOptions();
        o.Timeout.Should().Be(30_000);
        o.State.Should().Be(WaitState.Load);
    }

    [Theory]
    [InlineData(WaitState.Attached)]
    [InlineData(WaitState.Detached)]
    [InlineData(WaitState.Visible)]
    [InlineData(WaitState.Hidden)]
    [InlineData(WaitState.Load)]
    public void WaitState_Enum_HasValues(WaitState s)
    {
        s.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
