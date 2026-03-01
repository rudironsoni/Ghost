using FluentAssertions;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.Abstractions;

public class TypeOptionsTests : ReliabilityTestBase
{
    public TypeOptionsTests(ITestOutputHelper output) : base(output) { }

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
