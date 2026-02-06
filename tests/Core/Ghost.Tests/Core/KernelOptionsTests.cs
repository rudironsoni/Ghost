using FluentAssertions;
using Xunit;
namespace Ghost.Core.Tests;

public class KernelOptionsTests
{
    [Fact]
    public void CtorDefaultsAreExpected()
    {
        var opts = new KernelOptions();
        opts.Headless.Should().BeTrue();
        opts.SlowMo.Should().Be(0);
        opts.ProxyServer.Should().BeNull();
    }

    [Fact]
    public void PropertiesSetGetWorks()
    {
        var opts = new KernelOptions();
        opts.Headless = false;
        opts.SlowMo = 123;
        opts.ProxyServer = "http://1.2.3.4:8080";

        opts.Headless.Should().BeFalse();
        opts.SlowMo.Should().Be(123);
        opts.ProxyServer.Should().Be("http://1.2.3.4:8080");
    }
}
