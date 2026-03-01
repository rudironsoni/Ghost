using FluentAssertions;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.Abstractions;

public class WaitOptionsTests : ReliabilityTestBase
{
    public WaitOptionsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DefaultsAreExpected()
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
    public void WaitStateEnumHasValues(WaitState s)
    {
        s.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
