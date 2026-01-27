using FluentAssertions;
using Xunit;

namespace Ghostwright.Tests.Abstractions;

public class NavigationOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var o = new NavigationOptions();
        o.Timeout.Should().Be(30_000);
        o.WaitUntil.Should().Be(WaitUntil.Load);
    }

    [Theory]
    [InlineData(WaitUntil.Load)]
    [InlineData(WaitUntil.DomContentLoaded)]
    [InlineData(WaitUntil.NetworkIdle)]
    public void WaitUntil_EnumValues_Available(WaitUntil val)
    {
        val.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
