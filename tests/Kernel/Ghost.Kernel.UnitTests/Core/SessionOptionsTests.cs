using FluentAssertions;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.Tests;

public class SessionOptionsTests : ReliabilityTestBase
{
    public SessionOptionsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CtorDefaultsAreExpected()
    {
        var opts = new SessionOptions();
        opts.ViewportWidth.Should().Be(1280);
        opts.ViewportHeight.Should().Be(720);
        opts.UserAgent.Should().BeNull();
    }

    [Fact]
    public void PropertiesSetGetWorks()
    {
        var opts = new SessionOptions { ViewportWidth = 200, ViewportHeight = 100, UserAgent = "ua" };
        opts.ViewportWidth.Should().Be(200);
        opts.ViewportHeight.Should().Be(100);
        opts.UserAgent.Should().Be("ua");
    }
}
