using FluentAssertions;
using Ghost.Stealth.TLS;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Ghost.Tests.Stealth.TLS;

public class TLSFingerprintServiceTests
{
    private readonly ILogger<TLSFingerprintService> _logger;
    private readonly TLSFingerprintService _service;

    public TLSFingerprintServiceTests()
    {
        _logger = Substitute.For<ILogger<TLSFingerprintService>>();
        _service = new TLSFingerprintService(_logger);
    }

    [Fact]
    public void GenerateProfile_ReturnsValidProfile()
    {
        // Act
        var profile = _service.GenerateProfile();

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
        var profile = _service.GenerateProfile("chrome");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateProfile_WithFirefoxType_ReturnsFirefoxLikeProfile()
    {
        // Act
        var profile = _service.GenerateProfile("firefox");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateProfile_WithSafariType_ReturnsSafariLikeProfile()
    {
        // Act
        var profile = _service.GenerateProfile("safari");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateProfile_WithEdgeType_ReturnsEdgeLikeProfile()
    {
        // Act
        var profile = _service.GenerateProfile("edge");

        // Assert
        profile.Should().NotBeNull();
        profile.TLSVersion.Should().Be(771);
    }

    [Fact]
    public void GenerateProfile_MultipleCalls_ReturnsDifferentProfiles()
    {
        // Act
        var profile1 = _service.GenerateProfile();
        var profile2 = _service.GenerateProfile();
        var profile3 = _service.GenerateProfile();

        // Assert
        var hashes = new[]
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
        var args = TLSFingerprintService.GetTLSLaunchArgs("chrome");

        // Assert
        args.Should().NotBeEmpty();
        args.Should().Contain(arg => arg.Contains("AutomationControlled"));
    }

    [Fact]
    public void GetTLSLaunchArgs_WithEdge_ReturnsChromiumArgs()
    {
        // Act
        var args = TLSFingerprintService.GetTLSLaunchArgs("edge");

        // Assert
        args.Should().NotBeEmpty();
        args.Should().Contain(arg => arg.Contains("AutomationControlled"));
    }

    [Fact]
    public void GetTLSLaunchArgs_WithFirefox_ReturnsFirefoxArgs()
    {
        // Act
        var args = TLSFingerprintService.GetTLSLaunchArgs("firefox");

        // Assert
        args.Should().NotBeEmpty();
    }

    [Fact]
    public void GetTLSLaunchArgs_WithUnknownBrowser_ReturnsEmptyList()
    {
        // Act
        var args = TLSFingerprintService.GetTLSLaunchArgs("unknown");

        // Assert
        args.Should().BeEmpty();
    }
}
