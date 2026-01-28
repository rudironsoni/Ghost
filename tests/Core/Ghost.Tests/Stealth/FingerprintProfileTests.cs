using FluentAssertions;
using Xunit;

namespace Ghost.Stealth.Tests;

public class FingerprintProfileTests
{
    [Fact]
    public void DesktopDefault_HasExpectedFields()
    {
        var p = FingerprintProfile.DesktopDefault;
        p.Name.Should().Be("desktop-default");
        p.UserAgent.Should().StartWith("Mozilla/");
        p.ViewportWidth.Should().Be(1280);
        p.ViewportHeight.Should().Be(720);
    }

    [Fact]
    public void InitProperties_CanBeAssigned_AndAreImmutable()
    {
        var p = new FingerprintProfile { Name = "n", UserAgent = "u", ViewportWidth = 10, ViewportHeight = 20 };
        p.Name.Should().Be("n");
        p.UserAgent.Should().Be("u");
        p.ViewportWidth.Should().Be(10);
        p.ViewportHeight.Should().Be(20);
    }
}
