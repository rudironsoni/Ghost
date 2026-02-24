using FluentAssertions;
using Ghost.Stealth;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Stealth;

public class FingerprintGeneratorTests : ReliabilityTestBase
{
    public FingerprintGeneratorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GenerateReturnsCoherentProfile()
    {
        FingerprintProfile profile = FingerprintGenerator.Generate();

        profile.Should().NotBeNull();
        profile.UserAgent.Should().Contain(profile.ChromeVersion);
        profile.Platform.Should().NotBeNullOrEmpty();

        // Basic range checks
        profile.Cores.Should().BeGreaterThan(0);
        profile.MemoryGb.Should().BeGreaterThan(0);
        profile.BatteryLevel.Should().BeInRange(0.7, 1.0);
        profile.Rtt.Should().BeGreaterThan(0);
        profile.Downlink.Should().BeGreaterThan(0);

        // Geo checks (roughly NYC bounding box)
        profile.Latitude.Should().BeInRange(40.5, 41.0);
        profile.Longitude.Should().BeInRange(-74.3, -73.6);

        profile.VideoCardVendor.Should().NotBeNullOrEmpty();
        profile.VideoCardRenderer.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateProducesDifferentProfiles()
    {
        FingerprintProfile p1 = FingerprintGenerator.Generate();
        FingerprintProfile p2 = FingerprintGenerator.Generate();

        // Very unlikely to be identical given the seed randomization
        p1.Seed.Should().NotBe(p2.Seed);
        p1.Name.Should().NotBe(p2.Name);
    }
}
