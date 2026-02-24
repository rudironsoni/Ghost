using Ghost.Consent;
using Microsoft.Playwright;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Consent;

public class RegionDetectorTests : ReliabilityTestBase
{
    public RegionDetectorTests(ITestOutputHelper output) : base(output) { }

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
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.GetContentAsync()).ReturnsAsync("<html><body>This site uses GDPR compliant cookies</body></html>");

        // Act
        RegionDetector.PrivacyRegulation result = await RegionDetector.DetectRegulationAsync(mockPage.Object);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.GDPR, result);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithCcpaKeywords_ReturnsCCPA()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.GetContentAsync()).ReturnsAsync("<html><body>Do Not Sell My Personal Information (CCPA)</body></html>");

        // Act
        RegionDetector.PrivacyRegulation result = await RegionDetector.DetectRegulationAsync(mockPage.Object);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.CCPA, result);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithLgpdKeywords_ReturnsLGPD()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.GetContentAsync()).ReturnsAsync("<html><body>Lei Geral de Proteção de Dados (LGPD)</body></html>");

        // Act
        RegionDetector.PrivacyRegulation result = await RegionDetector.DetectRegulationAsync(mockPage.Object);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.LGPD, result);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithPipedaKeywords_ReturnsPIPEDA()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.GetContentAsync()).ReturnsAsync("<html><body>PIPEDA compliance statement</body></html>");

        // Act
        RegionDetector.PrivacyRegulation result = await RegionDetector.DetectRegulationAsync(mockPage.Object);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.PIPEDA, result);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithNoKeywords_ReturnsUnknown()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.GetContentAsync()).ReturnsAsync("<html><body>Welcome to our website</body></html>");
        mockPage.Setup(p => p.EvaluateAsync<bool>(It.IsAny<string>())).ReturnsAsync(false);
        mockPage.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>())).ReturnsAsync(string.Empty);

        // Act
        RegionDetector.PrivacyRegulation result = await RegionDetector.DetectRegulationAsync(mockPage.Object);

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
        string strategy = RegionDetector.GetConsentStrategy(regulation);

        // Assert
        Assert.NotNull(strategy);
        Assert.NotEmpty(strategy);
    }

    [Fact]
    public void GetConsentStrategy_ForGDPR_ContainsStrictKeyword()
    {
        // Act
        string strategy = RegionDetector.GetConsentStrategy(RegionDetector.PrivacyRegulation.GDPR);

        // Assert
        Assert.Contains("Strict", strategy);
    }

    [Fact]
    public void GetConsentStrategy_ForCCPA_ContainsOptOutKeyword()
    {
        // Act
        string strategy = RegionDetector.GetConsentStrategy(RegionDetector.PrivacyRegulation.CCPA);

        // Assert
        Assert.Contains("Opt-out", strategy);
    }

    [Fact]
    public async Task DetectRegulationAsync_WithException_ReturnsUnknown()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        mockPage.Setup(p => p.GetContentAsync()).ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        RegionDetector.PrivacyRegulation result = await RegionDetector.DetectRegulationAsync(mockPage.Object);

        // Assert
        Assert.Equal(RegionDetector.PrivacyRegulation.Unknown, result);
    }
}
