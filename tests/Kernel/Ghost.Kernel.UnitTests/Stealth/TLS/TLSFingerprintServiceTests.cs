using FluentAssertions;
using Ghost.Stealth.TLS;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Stealth.TLS;

public class TLSFingerprintServiceTests : ReliabilityTestBase
{
    private readonly Mock<ILogger<TLSFingerprintService>> _mockLogger;
    private readonly TLSFingerprintService _service;

    public TLSFingerprintServiceTests(ITestOutputHelper output) : base(output)
    {
        _mockLogger = new Mock<ILogger<TLSFingerprintService>>();
        _service = new TLSFingerprintService(_mockLogger.Object);
    }

    [Fact]
    public void GenerateProfile_ReturnsValidProfile()
    {
        // Act
        JA3Profile profile = _service.GenerateProfile();

        // Assert
        profile.Should().NotBeNull();
        profile.CipherSuites.Should().NotBeEmpty();
        profile.Extensions.Should().NotBeEmpty();
        profile.ToJA3Hash().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateProfile_WithChromeType_ReturnsChromeLikeProfile()
    {
        // Act
        JA3Profile profile = _service.GenerateProfile("chrome");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateProfile_WithFirefoxType_ReturnsFirefoxLikeProfile()
    {
        // Act
        JA3Profile profile = _service.GenerateProfile("firefox");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateProfile_WithSafariType_ReturnsSafariLikeProfile()
    {
        // Act
        JA3Profile profile = _service.GenerateProfile("safari");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateProfile_WithEdgeType_ReturnsEdgeLikeProfile()
    {
        // Act
        JA3Profile profile = _service.GenerateProfile("edge");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateProfile_MultipleCalls_ReturnsDifferentProfiles()
    {
        // Act
        JA3Profile profile1 = _service.GenerateProfile();
        JA3Profile profile2 = _service.GenerateProfile();
        JA3Profile profile3 = _service.GenerateProfile();

        // Assert
        string[] hashes = new[]
        {
            profile1.ToJA3Hash(),
            profile2.ToJA3Hash(),
            profile3.ToJA3Hash()
        };

        hashes.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void GetTLSLaunchArgs_WithChrome_ReturnsChromiumArgs()
    {
        // Act
        IReadOnlyList<string> args = TLSFingerprintService.GetTLSLaunchArgs("chrome");

        // Assert
        args.Should().NotBeEmpty();
        args.Should().Contain(arg => arg.Contains("AutomationControlled"));
    }

    [Fact]
    public void GetTLSLaunchArgs_WithEdge_ReturnsChromiumArgs()
    {
        // Act
        IReadOnlyList<string> args = TLSFingerprintService.GetTLSLaunchArgs("edge");

        // Assert
        args.Should().NotBeEmpty();
        args.Should().Contain(arg => arg.Contains("AutomationControlled"));
    }

    [Fact]
    public void GetTLSLaunchArgs_WithFirefox_ReturnsFirefoxArgs()
    {
        // Act
        IReadOnlyList<string> args = TLSFingerprintService.GetTLSLaunchArgs("firefox");

        // Assert
        args.Should().NotBeEmpty();
    }

    [Fact]
    public void GetTLSLaunchArgs_WithUnknownBrowser_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<string> args = TLSFingerprintService.GetTLSLaunchArgs("unknown");

        // Assert
        args.Should().BeEmpty();
    }
}
