using FluentAssertions;
using Ghost.Stealth.TLS;
using Xunit;

namespace Ghost.Tests.Stealth.TLS;

public class JA3RandomizerTests
{
    [Fact]
    public void GenerateRandomProfile_ReturnsValidProfile()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        JA3Profile profile = randomizer.GenerateRandomProfile();

        // Assert
        profile.Should().NotBeNull();
        profile.CipherSuites.Should().NotBeEmpty();
        profile.Extensions.Should().NotBeEmpty();
        profile.EllipticCurves.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateRandomProfile_WithChromeHint_ReturnsChromeLikeProfile()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        JA3Profile profile = randomizer.GenerateRandomProfile("chrome");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateRandomProfile_WithFirefoxHint_ReturnsFirefoxLikeProfile()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        JA3Profile profile = randomizer.GenerateRandomProfile("firefox");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateRandomProfile_MultipleCalls_ReturnsDifferentHashes()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        string hash1 = randomizer.GenerateRandomProfile().ToJA3Hash();
        string hash2 = randomizer.GenerateRandomProfile().ToJA3Hash();
        string hash3 = randomizer.GenerateRandomProfile().ToJA3Hash();

        // Assert - At least some should be different
        string[] hashes = new[] { hash1, hash2, hash3 };
        hashes.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void GenerateRandomProfile_PreservesTLS13Ciphers()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        JA3Profile profile = randomizer.GenerateRandomProfile();
        var tls13Ciphers = profile.CipherSuites.Where(c => c >= 4865 && c <= 4867).ToList();

        // Assert
        tls13Ciphers.Should().NotBeEmpty("TLS 1.3 ciphers should be preserved");
    }

    [Fact]
    public void GenerateRandomProfile_PreservesServerNameExtension()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        JA3Profile profile = randomizer.GenerateRandomProfile();

        // Assert
        profile.Extensions.Should().Contain(0, "server_name extension must be present");
    }

    [Fact]
    public void GenerateRandomProfile_ServerNameExtensionIsFirst()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        JA3Profile profile = randomizer.GenerateRandomProfile();

        // Assert
        profile.Extensions[0].Should().Be(0, "server_name extension must be first");
    }

    [Fact]
    public void GenerateMultipleProfiles_GeneratesRequestedCount()
    {
        // Arrange
        var randomizer = new JA3Randomizer();
        const int count = 10;

        // Act
        IReadOnlyList<JA3Profile> profiles = randomizer.GenerateMultipleProfiles(count);

        // Assert
        profiles.Should().HaveCount(count);
    }

    [Fact]
    public void GenerateMultipleProfiles_GeneratesUniqueHashes()
    {
        // Arrange
        var randomizer = new JA3Randomizer();
        const int count = 100;

        // Act
        IReadOnlyList<JA3Profile> profiles = randomizer.GenerateMultipleProfiles(count);
        var hashes = profiles.Select(p => p.ToJA3Hash()).ToList();
        int uniqueCount = hashes.Distinct().Count();

        // Assert - Test statistical distribution: expect >95% unique hashes
        double uniqueRatio = uniqueCount / (double)count;
        uniqueRatio.Should().BeGreaterThan(0.95,
            $"Expected >95% unique hashes from random generation, got {uniqueRatio:P2} ({uniqueCount}/{count} unique)");
    }

    [Fact]
    public void GenerateMultipleProfiles_WithZeroCount_ThrowsException()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        Func<IReadOnlyList<JA3Profile>> act = () => randomizer.GenerateMultipleProfiles(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GenerateMultipleProfiles_WithNegativeCount_ThrowsException()
    {
        // Arrange
        var randomizer = new JA3Randomizer();

        // Act
        Func<IReadOnlyList<JA3Profile>> act = () => randomizer.GenerateMultipleProfiles(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithSeed_ProducesDeterministicResults()
    {
        // Arrange
        const int seed = 42;
        var randomizer1 = new JA3Randomizer(seed);
        var randomizer2 = new JA3Randomizer(seed);

        // Act
        JA3Profile profile1 = randomizer1.GenerateRandomProfile();
        JA3Profile profile2 = randomizer2.GenerateRandomProfile();

        // Assert
        profile1.ToJA3Hash().Should().Be(profile2.ToJA3Hash());
    }

    [Fact]
    public void GenerateRandomProfile_AllBrowserHints_ProduceValidProfiles()
    {
        // Arrange
        var randomizer = new JA3Randomizer();
        string[] browsers = new[] { "chrome", "firefox", "safari", "edge" };

        // Act & Assert
        foreach (string? browser in browsers)
        {
            JA3Profile profile = randomizer.GenerateRandomProfile(browser);
            profile.Should().NotBeNull();
            profile.CipherSuites.Should().NotBeEmpty();
            profile.Extensions.Should().NotBeEmpty();
            profile.ToJA3Hash().Should().NotBeNullOrEmpty();
        }
    }
}
