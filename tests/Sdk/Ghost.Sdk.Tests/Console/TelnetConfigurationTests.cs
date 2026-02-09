using FluentAssertions;
using Ghost.Sdk.Console;
using Xunit;

namespace Ghost.Sdk.Tests.Console;

/// <summary>
/// Unit tests for TelnetConfiguration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TelnetConfigurationTests
{
    [Fact]
    public void DefaultConfiguration_HasSecureDefaults()
    {
        // Arrange & Act
        var config = new TelnetConfiguration();

        // Assert
        config.Enabled.Should().BeFalse("security - disabled by default");
        config.Port.Should().Be(6023, "Scrapy default port");
        config.BindAddress.Should().Be("127.0.0.1", "localhost only for security");
        config.Username.Should().BeNull();
        config.Password.Should().BeNull();
        config.AllowedIps.Should().BeEmpty();
        config.SessionTimeout.Should().Be(TimeSpan.FromMinutes(30));
        config.MaxConnections.Should().Be(5);
        config.EnableCommandHistory.Should().BeTrue();
        config.MaxHistorySize.Should().Be(100);
        config.LogCommands.Should().BeTrue();
        config.AllowPauseResume.Should().BeTrue();
        config.AllowShutdown.Should().BeTrue();
        config.AllowQueueInspection.Should().BeTrue();
        config.AllowStatsInspection.Should().BeTrue();
        config.CustomCommands.Should().BeEmpty();
    }

    [Fact]
    public void Configuration_CanBeModified()
    {
        // Arrange
        var config = new TelnetConfiguration();

        // Act
        config.Enabled = true;
        config.Port = 7000;
        config.BindAddress = "0.0.0.0";
        config.Username = "admin";
        config.Password = "secret";
        config.AllowedIps.Add("192.168.1.0/24");
        config.SessionTimeout = TimeSpan.FromMinutes(60);
        config.MaxConnections = 10;
        config.EnableCommandHistory = false;
        config.MaxHistorySize = 200;
        config.LogCommands = false;
        config.AllowPauseResume = false;
        config.AllowShutdown = false;
        config.AllowQueueInspection = false;
        config.AllowStatsInspection = false;
        config.CustomCommands["custom"] = "Custom.Command";

        // Assert
        config.Enabled.Should().BeTrue();
        config.Port.Should().Be(7000);
        config.BindAddress.Should().Be("0.0.0.0");
        config.Username.Should().Be("admin");
        config.Password.Should().Be("secret");
        config.AllowedIps.Should().ContainSingle("192.168.1.0/24");
        config.SessionTimeout.Should().Be(TimeSpan.FromMinutes(60));
        config.MaxConnections.Should().Be(10);
        config.EnableCommandHistory.Should().BeFalse();
        config.MaxHistorySize.Should().Be(200);
        config.LogCommands.Should().BeFalse();
        config.AllowPauseResume.Should().BeFalse();
        config.AllowShutdown.Should().BeFalse();
        config.AllowQueueInspection.Should().BeFalse();
        config.AllowStatsInspection.Should().BeFalse();
        config.CustomCommands.Should().ContainKey("custom");
    }
}
