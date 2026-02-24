using FluentAssertions;
using Ghost.Stealth.TLS;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Stealth.TLS;

public class JA3ProfileTests : ReliabilityTestBase
{
    public JA3ProfileTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ToJA3String_WithValidProfile_ReturnsCorrectFormat()
    {
        // Arrange
        var profile = new JA3Profile
        {
            TLSVersion = 771,
            CipherSuites = [4865, 4866, 4867],
            Extensions = [0, 23, 65281],
            EllipticCurves = [29, 23, 24],
            ECPointFormats = [0]
        };

        // Act
        string ja3String = profile.ToJA3String();

        // Assert
        ja3String.Should().Be("771,4865-4866-4867,0-23-65281,29-23-24,0");
    }

    [Fact]
    public void ToJA3Hash_WithValidProfile_ReturnsMD5Hash()
    {
        // Arrange
        var profile = new JA3Profile
        {
            TLSVersion = 771,
            CipherSuites = [4865, 4866, 4867],
            Extensions = [0, 23, 65281],
            EllipticCurves = [29, 23, 24],
            ECPointFormats = [0]
        };

        // Act
        string hash = profile.ToJA3Hash();

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(32); // MD5 hash is 32 hex characters
        hash.Should().MatchRegex("^[a-f0-9]{32}$"); // Lowercase hex
    }

    [Fact]
    public void ToJA3Hash_SameProfile_ReturnsSameHash()
    {
        // Arrange
        var profile1 = new JA3Profile
        {
            TLSVersion = 771,
            CipherSuites = [4865, 4866],
            Extensions = [0, 23],
            EllipticCurves = [29],
            ECPointFormats = [0]
        };

        var profile2 = new JA3Profile
        {
            TLSVersion = 771,
            CipherSuites = [4865, 4866],
            Extensions = [0, 23],
            EllipticCurves = [29],
            ECPointFormats = [0]
        };

        // Act
        string hash1 = profile1.ToJA3Hash();
        string hash2 = profile2.ToJA3Hash();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ToJA3Hash_DifferentProfiles_ReturnDifferentHashes()
    {
        // Arrange
        var profile1 = new JA3Profile
        {
            TLSVersion = 771,
            CipherSuites = [4865, 4866],
            Extensions = [0, 23],
            EllipticCurves = [29],
            ECPointFormats = [0]
        };

        var profile2 = new JA3Profile
        {
            TLSVersion = 771,
            CipherSuites = [4866, 4865], // Reversed order
            Extensions = [0, 23],
            EllipticCurves = [29],
            ECPointFormats = [0]
        };

        // Act
        string hash1 = profile1.ToJA3Hash();
        string hash2 = profile2.ToJA3Hash();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        // Arrange
        var original = new JA3Profile
        {
            TLSVersion = 771,
            CipherSuites = [4865, 4866],
            Extensions = [0, 23],
            EllipticCurves = [29],
            ECPointFormats = [0]
        };

        // Act
        JA3Profile clone = original.Clone();
        clone.CipherSuites.Add(4867);

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.CipherSuites.Should().HaveCount(3);
        original.CipherSuites.Should().HaveCount(2);
    }

    [Fact]
    public void ToJA3String_EmptyLists_ReturnsValidFormat()
    {
        // Arrange
        var profile = new JA3Profile
        {
            TLSVersion = 771,
            CipherSuites = [],
            Extensions = [],
            EllipticCurves = [],
            ECPointFormats = []
        };

        // Act
        string ja3String = profile.ToJA3String();

        // Assert
        ja3String.Should().Be("771,,,,");
    }
}
