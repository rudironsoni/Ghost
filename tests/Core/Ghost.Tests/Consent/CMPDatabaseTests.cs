using Ghost.Consent;
using Xunit;

namespace Ghost.Tests.Consent;

public class CMPDatabaseTests
{
    [Fact]
    public void GetAllConfigs_ReturnsNonEmptyList()
    {
        // Act
        var configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.NotNull(configs);
        Assert.NotEmpty(configs);
    }

    [Fact]
    public void GetAllConfigs_ReturnsAtLeast10Configs()
    {
        // Act
        var configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.True(configs.Count >= 10, $"Expected at least 10 CMP configs, but found {configs.Count}");
    }

    [Theory]
    [InlineData("onetrust-cookiepro")]
    [InlineData("cookiebot")]
    [InlineData("cookieyes")]
    [InlineData("usercentrics")]
    [InlineData("quantcast")]
    [InlineData("didomi")]
    [InlineData("cookiefirst")]
    [InlineData("osano")]
    [InlineData("trustarc")]
    [InlineData("sourcepoint")]
    public void GetConfig_WithValidName_ReturnsConfig(string name)
    {
        // Act
        var config = CMPDatabase.GetConfig(name);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(name, config.Name);
    }

    [Fact]
    public void GetConfig_WithUnknownName_ReturnsNull()
    {
        // Act
        var config = CMPDatabase.GetConfig("unknown-cmp");

        // Assert
        Assert.Null(config);
    }

    [Fact]
    public void GetConfig_IsCaseInsensitive()
    {
        // Act
        var config1 = CMPDatabase.GetConfig("onetrust-cookiepro");
        var config2 = CMPDatabase.GetConfig("ONETRUST-COOKIEPRO");
        var config3 = CMPDatabase.GetConfig("OneTrust-CookiePro");

        // Assert
        Assert.NotNull(config1);
        Assert.NotNull(config2);
        Assert.NotNull(config3);
        Assert.Equal(config1.Name, config2.Name);
        Assert.Equal(config1.Name, config3.Name);
    }

    [Fact]
    public void GetAllConfigs_EachConfigHasRequiredProperties()
    {
        // Act
        var configs = CMPDatabase.GetAllConfigs();

        // Assert
        foreach (var config in configs)
        {
            Assert.NotNull(config.Name);
            Assert.NotEmpty(config.Name);
            Assert.NotNull(config.Detectors);
            Assert.NotEmpty(config.Detectors);
            Assert.NotNull(config.AcceptButton);
            Assert.NotEmpty(config.AcceptButton);
        }
    }

    [Fact]
    public void GetAllConfigs_ContainsOneTrustVariants()
    {
        // Act
        var configs = CMPDatabase.GetAllConfigs();
        var oneTrustConfigs = configs.Where(c => c.Name.Contains("onetrust", StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert
        Assert.NotEmpty(oneTrustConfigs);
        Assert.Contains(oneTrustConfigs, c => c.Name == "onetrust-cookiepro");
    }

    [Fact]
    public void GetAllConfigs_ContainsCookieBot()
    {
        // Act
        var configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.Contains(configs, c => c.Name == "cookiebot");
    }

    [Fact]
    public void GetAllConfigs_ContainsCookieYes()
    {
        // Act
        var configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.Contains(configs, c => c.Name == "cookieyes");
    }

    [Fact]
    public void GetAllConfigs_ContainsGenericFallbacks()
    {
        // Act
        var configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.Contains(configs, c => c.Name == "generic-accept");
        Assert.Contains(configs, c => c.Name == "generic-iframe");
    }

    [Fact]
    public void GetAllConfigs_OneTrustHasCorrectSelectors()
    {
        // Act
        var config = CMPDatabase.GetConfig("onetrust-cookiepro");

        // Assert
        Assert.NotNull(config);
        Assert.Contains("#onetrust-banner-sdk", config.Detectors);
        Assert.Equal(".cmp-button__accept", config.AcceptButton);
    }

    [Fact]
    public void GetAllConfigs_CookieBotHasCorrectSelectors()
    {
        // Act
        var config = CMPDatabase.GetConfig("cookiebot");

        // Assert
        Assert.NotNull(config);
        Assert.Contains("#CybotCookiebotDialog", config.Detectors);
        Assert.Equal("#CybotCookiebotDialogBodyLevelButtonLevelOptinAllowAll", config.AcceptButton);
    }
}
