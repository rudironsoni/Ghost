using FluentAssertions;
using Xunit;

namespace Ghostwright.Core.Tests;

public class SessionOptionsTests
{
    [Fact]
    public void Ctor_Defaults_AreExpected()
    {
        var opts = new SessionOptions();
        opts.ViewportWidth.Should().Be(1280);
        opts.ViewportHeight.Should().Be(720);
        opts.UserAgent.Should().BeNull();
    }

    [Fact]
    public void Properties_SetGet_Works()
    {
        var opts = new SessionOptions { ViewportWidth = 200, ViewportHeight = 100, UserAgent = "ua" };
        opts.ViewportWidth.Should().Be(200);
        opts.ViewportHeight.Should().Be(100);
        opts.UserAgent.Should().Be("ua");
    }
}
