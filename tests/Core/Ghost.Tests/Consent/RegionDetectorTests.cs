using Ghost.Consent;
using Microsoft.Playwright;
using NSubstitute;
using Xunit;

namespace Ghost.Tests.Consent;

public class RegionDetectorTests
{
    [Fact]
    public async Task DetectRegulationAsync_WithNullPage_ThrowsArgumentNullException()
    {
        // Arrange
        IPage? page = null;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await RegionDetector.DetectRegulationAsync(page));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task DetectRegulationAsync_WithGdprKeywords_ReturnsGDPR()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.GetContentAsync().Returns("<html><body>This site uses GDPR compliant cookies</body></html>");

        // Act
        var result = await RegionDetector.DetectRegulationAsync(page);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.GDPR, result);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithCcpaKeywords_ReturnsCCPA()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.GetContentAsync().Returns("<html><body>Do Not Sell My Personal Information (CCPA)</body></html>");

        // Act
        var result = await RegionDetector.DetectRegulationAsync(page);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.CCPA, result);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithLgpdKeywords_ReturnsLGPD()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.GetContentAsync().Returns("<html><body>Lei Geral de Proteção de Dados (LGPD)</body></html>");

        // Act
        var result = await RegionDetector.DetectRegulationAsync(page);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.LGPD, result);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithPipedaKeywords_ReturnsPIPEDA()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.GetContentAsync().Returns("<html><body>PIPEDA compliance statement</body></html>");

        // Act
        var result = await RegionDetector.DetectRegulationAsync(page);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.PIPEDA, result);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithNoKeywords_ReturnsUnknown()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.GetContentAsync().Returns("<html><body>Welcome to our website</body></html>");
        page.EvaluateAsync<bool>(Arg.Any<string>()).Returns(false);
        page.EvaluateAsync<string>(Arg.Any<string>()).Returns(string.Empty);

        // Act
        var result = await RegionDetector.DetectRegulationAsync(page);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.Unknown, result);
    }

    [Theory]
    [InlineData(RegionDetector.PrivacyRegulation.GDPR)]
    [InlineData(RegionDetector.PrivacyRegulation.CCPA)]
    [InlineData(RegionDetector.PrivacyRegulation.LGPD)]
    [InlineData(RegionDetector.PrivacyRegulation.PIPEDA)]
    [InlineData(RegionDetector.PrivacyRegulation.Other)]
    [InlineData(RegionDetector.PrivacyRegulation.Unknown)]
    public void GetConsentStrategy_ReturnsNonEmptyString(RegionDetector.PrivacyRegulation regulation)
    {
        // Act
        var strategy = RegionDetector.GetConsentStrategy(regulation);

        // Assert
        Assert.NotNull(strategy);
        Assert.NotEmpty(strategy);
    }

    [Fact]
    public void GetConsentStrategy_ForGDPR_ContainsStrictKeyword()
    {
        // Act
        var strategy = RegionDetector.GetConsentStrategy(RegionDetector.PrivacyRegulation.GDPR);

        // Assert
        Assert.Contains("Strict", strategy);
    }

    [Fact]
    public void GetConsentStrategy_ForCCPA_ContainsOptOutKeyword()
    {
        // Act
        var strategy = RegionDetector.GetConsentStrategy(RegionDetector.PrivacyRegulation.CCPA);

        // Assert
        Assert.Contains("Opt-out", strategy);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithException_ReturnsUnknown()
    {
        // Arrange
        var page = Substitute.For<IPage>();
        page.GetContentAsync().Returns(Task.FromException<string>(new InvalidOperationException("Test error")));

        // Act
        var result = await RegionDetector.DetectRegulationAsync(page);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.Unknown, result);
    }
}
