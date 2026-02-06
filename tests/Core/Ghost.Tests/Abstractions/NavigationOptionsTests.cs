using FluentAssertions;
using Xunit;

namespace Ghost.Tests.Abstractions;

public class NavigationOptionsTests
{
    [Fact]
    public void DefaultsAreExpected()
    {
        var o = new NavigationOptions();
        o.Timeout.Should().Be(30_000);
        o.WaitUntil.Should().Be(WaitUntil.Load);
    }

    [Theory]
    [InlineData(WaitUntil.Load)]
    [InlineData(WaitUntil.DomContentLoaded)]
    [InlineData(WaitUntil.NetworkIdle)]
    public void WaitUntilEnumValuesAvailable(WaitUntil val)
    {
        val.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
