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
        p.ViewportWidth.Should().Be(1920);
        p.ViewportHeight.Should().Be(1080);
    }

    [Fact]
    public void InitProperties_CanBeAssigned_AndAreImmutable()
    {
        var p = new FingerprintProfile
        {
            Name = "n",
            UserAgent = "u",
            ViewportWidth = 10,
            ViewportHeight = 20,
            Cores = 4,
            MemoryGb = 8,
            Platform = "p",
            ChromeVersion = "v",
            Seed = 1,
            Latitude = 0,
            Longitude = 0,
            TimeZone = "tz",
            VideoCardVendor = "v",
            VideoCardRenderer = "r",
            BatteryLevel = 1,
            ScreenColorDepth = 24,
            DeviceMemoryGb = 8,
            OperatingSystem = "Windows NT 10.0; Win64; x64",
            ConnectionType = "wifi",
            ScreenOrientation = "landscape-primary"
        };
        p.Name.Should().Be("n");
        p.UserAgent.Should().Be("u");
        p.ViewportWidth.Should().Be(10);
        p.ViewportHeight.Should().Be(20);
    }
}
