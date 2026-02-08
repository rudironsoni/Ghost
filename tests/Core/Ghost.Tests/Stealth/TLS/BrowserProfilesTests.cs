using FluentAssertions;
using Ghost.Stealth.TLS;
using Xunit;

namespace Ghost.Tests.Stealth.TLS;

public class BrowserProfilesTests
{
    [Fact]
    public void Chrome120_HasValidProfile()
    {
        // Act
        var profile = BrowserProfiles.Chrome120;

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
        profile.CipherSuites.Should().NotBeEmpty();
        profile.Extensions.Should().NotBeEmpty();
        profile.EllipticCurves.Should().NotBeEmpty();
        profile.ECPointFormats.Should().NotBeEmpty();
    }

    [Fact]
    public void Firefox121_HasValidProfile()
    {
        // Act
        var profile = BrowserProfiles.Firefox121;

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
        profile.CipherSuites.Should().NotBeEmpty();
        profile.Extensions.Should().NotBeEmpty();
        profile.EllipticCurves.Should().NotBeEmpty();
    }

    [Fact]
    public void Safari17_HasValidProfile()
    {
        // Act
        var profile = BrowserProfiles.Safari17;

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
        profile.CipherSuites.Should().NotBeEmpty();
        profile.Extensions.Should().NotBeEmpty();
        profile.EllipticCurves.Should().NotBeEmpty();
    }

    [Fact]
    public void Edge_HasValidProfile()
    {
        // Act
        var profile = BrowserProfiles.Edge;

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
        profile.CipherSuites.Should().NotBeEmpty();
        profile.Extensions.Should().NotBeEmpty();
        profile.EllipticCurves.Should().NotBeEmpty();
    }

    [Fact]
    public void AllProfiles_ContainsFourBrowsers()
    {
        // Act
        var profiles = BrowserProfiles.AllProfiles;

        // Assert
        profiles.Should().HaveCount(4);
    }

    [Fact]
    public void GetRandomProfile_ReturnsValidProfile()
    {
        // Act
        var profile = BrowserProfiles.GetRandomProfile();

        // Assert
        profile.Should().NotBeNull();
        profile.CipherSuites.Should().NotBeEmpty();
        profile.Extensions.Should().NotBeEmpty();
    }

    [Fact]
    public void GetRandomProfile_WithSeed_ReturnsDeterministicResult()
    {
        // Arrange
        var seed = 42;
        var random1 = new Random(seed);
        var random2 = new Random(seed);

        // Act
        var profile1 = BrowserProfiles.GetRandomProfile(random1);
        var profile2 = BrowserProfiles.GetRandomProfile(random2);

        // Assert
        profile1.ToJA3Hash().Should().Be(profile2.ToJA3Hash());
    }

    [Fact]
    public void BrowserProfiles_HaveTLS13Ciphers()
    {
        // Arrange
        var tls13CipherRange = Enumerable.Range(4865, 3); // 4865, 4866, 4867

        // Act & Assert
        BrowserProfiles.Chrome120.CipherSuites
            .Should().Contain(c => tls13CipherRange.Contains(c));
        BrowserProfiles.Firefox121.CipherSuites
            .Should().Contain(c => tls13CipherRange.Contains(c));
        BrowserProfiles.Safari17.CipherSuites
            .Should().Contain(c => tls13CipherRange.Contains(c));
        BrowserProfiles.Edge.CipherSuites
            .Should().Contain(c => tls13CipherRange.Contains(c));
    }

    [Fact]
    public void BrowserProfiles_HaveServerNameExtension()
    {
        // Arrange
        const int serverNameExtension = 0;

        // Act & Assert
        BrowserProfiles.Chrome120.Extensions.Should().Contain(serverNameExtension);
        BrowserProfiles.Firefox121.Extensions.Should().Contain(serverNameExtension);
        BrowserProfiles.Safari17.Extensions.Should().Contain(serverNameExtension);
        BrowserProfiles.Edge.Extensions.Should().Contain(serverNameExtension);
    }

    [Fact]
    public void BrowserProfiles_GenerateDifferentHashes()
    {
        // Act
        var chromeHash = BrowserProfiles.Chrome120.ToJA3Hash();
        var firefoxHash = BrowserProfiles.Firefox121.ToJA3Hash();
        var safariHash = BrowserProfiles.Safari17.ToJA3Hash();
        var edgeHash = BrowserProfiles.Edge.ToJA3Hash();

        // Assert - Edge and Chrome may share fingerprints (both Chromium-based)
        // but Firefox and Safari should be distinct from each other and from Chromium browsers
        var hashes = new[] { chromeHash, firefoxHash, safariHash, edgeHash };
        var uniqueCount = hashes.Distinct().Count();

        // Expect at least 3 unique hashes (acknowledging Chrome/Edge similarity)
        uniqueCount.Should().BeGreaterOrEqualTo(3,
            "Expected at least 3 unique browser fingerprints (Chrome and Edge may be identical as both are Chromium-based)");

        // Firefox and Safari should be distinct from each other
        firefoxHash.Should().NotBe(safariHash, "Firefox and Safari should have different fingerprints");
    }
}
