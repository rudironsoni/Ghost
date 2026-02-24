using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Abstractions;

public class PageOptionsTests : ReliabilityTestBase
{
    public PageOptionsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DefaultsAreExpected()
    {
        var o = new PageOptions();
        o.Width.Should().Be(1280);
        o.Height.Should().Be(720);
        o.UserAgent.Should().BeNull();
        o.JavaScriptEnabled.Should().BeTrue();
    }

    [Fact]
    public void PropertiesCanBeSet()
    {
        var o = new PageOptions { Width = 300, Height = 400, UserAgent = "x", JavaScriptEnabled = false };
        o.Width.Should().Be(300);
        o.Height.Should().Be(400);
        o.UserAgent.Should().Be("x");
        o.JavaScriptEnabled.Should().BeFalse();
    }
}
