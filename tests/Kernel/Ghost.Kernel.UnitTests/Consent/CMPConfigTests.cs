using Ghost.Consent;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Consent;

public class CMPConfigTests : ReliabilityTestBase
{
    public CMPConfigTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CMPConfig_CanBeCreated()
    {
        // Act
        var config = new CMPConfig
        {
            Name = "test-cmp",
            Detectors = [".test-banner"],
            AcceptButton = ".test-accept"
        };

        // Assert
        Assert.Equal("test-cmp", config.Name);
        Assert.Single(config.Detectors);
        Assert.Equal(".test-banner", config.Detectors[0]);
        Assert.Equal(".test-accept", config.AcceptButton);
    }

    [Fact]
    public void CMPConfig_WithMultipleDetectors()
    {
        // Act
        var config = new CMPConfig
        {
            Name = "test-cmp",
            Detectors = [".test-banner", "#test-dialog", "[data-test]"],
            AcceptButton = ".test-accept"
        };

        // Assert
        Assert.Equal(3, config.Detectors.Length);
        Assert.Contains(".test-banner", config.Detectors);
        Assert.Contains("#test-dialog", config.Detectors);
        Assert.Contains("[data-test]", config.Detectors);
    }

    [Fact]
    public void CMPConfig_WithAlternativeSelectors()
    {
        // Act
        var config = new CMPConfig
        {
            Name = "test-cmp",
            Detectors = [".test-banner"],
            AcceptButton = ".test-accept",
            AlternativeAcceptSelectors = [".alt-accept", "#accept-all"]
        };

        // Assert
        Assert.NotNull(config.AlternativeAcceptSelectors);
        Assert.Equal(2, config.AlternativeAcceptSelectors.Length);
        Assert.Contains(".alt-accept", config.AlternativeAcceptSelectors);
    }

    [Fact]
    public void CMPConfig_WithMultiStep()
    {
        // Act
        var config = new CMPConfig
        {
            Name = "test-cmp",
            Detectors = [".test-banner"],
            AcceptButton = ".test-accept",
            MultiStep = true,
            Steps = [".step1", ".step2", ".step3"]
        };

        // Assert
        Assert.True(config.MultiStep);
        Assert.NotNull(config.Steps);
        Assert.Equal(3, config.Steps.Length);
    }

    [Fact]
    public void CMPConfig_WithIframe()
    {
        // Act
        var config = new CMPConfig
        {
            Name = "test-cmp",
            Detectors = ["iframe[src*='consent']"],
            AcceptButton = ".test-accept",
            IsIframe = true
        };

        // Assert
        Assert.True(config.IsIframe);
    }

    [Fact]
    public void CMPConfig_DefaultsToNonMultiStep()
    {
        // Act
        var config = new CMPConfig
        {
            Name = "test-cmp",
            Detectors = [".test-banner"],
            AcceptButton = ".test-accept"
        };

        // Assert
        Assert.False(config.MultiStep);
    }

    [Fact]
    public void CMPConfig_DefaultsToNonIframe()
    {
        // Act
        var config = new CMPConfig
        {
            Name = "test-cmp",
            Detectors = [".test-banner"],
            AcceptButton = ".test-accept"
        };

        // Assert
        Assert.False(config.IsIframe);
    }
}
