using Ghost.Consent;
using Xunit;

namespace Ghost.Tests.Consent;

public class CMPDatabaseTests
{
    [Fact]
    public void GetAllConfigs_ReturnsNonEmptyList()
    {
        // Act
        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.NotNull(configs);
        Assert.NotEmpty(configs);
    }

    [Fact]
    public void GetAllConfigs_ReturnsAtLeast25Configs()
    {
        // Act
        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.True(configs.Count >= 25, $"Expected at least 25 CMP configs, but found {configs.Count}");
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
        CMPConfig? config = CMPDatabase.GetConfig(name);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(name, config.Name);
    }

    [Fact]
    public void GetConfig_WithUnknownName_ReturnsNull()
    {
        // Act
        CMPConfig? config = CMPDatabase.GetConfig("unknown-cmp");

        // Assert
        Assert.Null(config);
    }

    [Fact]
    public void GetConfig_IsCaseInsensitive()
    {
        // Act
        CMPConfig? config1 = CMPDatabase.GetConfig("onetrust-cookiepro");
        CMPConfig? config2 = CMPDatabase.GetConfig("ONETRUST-COOKIEPRO");
        CMPConfig? config3 = CMPDatabase.GetConfig("OneTrust-CookiePro");

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
        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();

        // Assert
        foreach (CMPConfig config in configs)
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
        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();
        var oneTrustConfigs = configs.Where(c => c.Name.Contains("onetrust", StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert
        Assert.NotEmpty(oneTrustConfigs);
        Assert.Contains(oneTrustConfigs, c => c.Name == "onetrust-cookiepro");
    }

    [Fact]
    public void GetAllConfigs_ContainsCookieBot()
    {
        // Act
        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.Contains(configs, c => c.Name == "cookiebot");
    }

    [Fact]
    public void GetAllConfigs_ContainsCookieYes()
    {
        // Act
        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.Contains(configs, c => c.Name == "cookieyes");
    }

    [Fact]
    public void GetAllConfigs_ContainsGenericFallbacks()
    {
        // Act
        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();

        // Assert
        Assert.Contains(configs, c => c.Name == "generic-accept");
        Assert.Contains(configs, c => c.Name == "generic-iframe");
    }

    [Fact]
    public void GetAllConfigs_OneTrustHasCorrectSelectors()
    {
        // Act
        CMPConfig? config = CMPDatabase.GetConfig("onetrust-cookiepro");

        // Assert
        Assert.NotNull(config);
        Assert.Contains("#onetrust-banner-sdk", config.Detectors);
        Assert.Equal(".cmp-button__accept", config.AcceptButton);
    }

    [Fact]
    public void GetAllConfigs_CookieBotHasCorrectSelectors()
    {
        // Act
        CMPConfig? config = CMPDatabase.GetConfig("cookiebot");

        // Assert
        Assert.NotNull(config);
        Assert.Contains("#CybotCookiebotDialog", config.Detectors);
        Assert.Equal("#CybotCookiebotDialogBodyLevelButtonLevelOptinAllowAll", config.AcceptButton);
    }
}
