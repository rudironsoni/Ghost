using FluentAssertions;
using Ghost.Sdk.Console;
using Xunit;

namespace Ghost.Sdk.Tests.Console;

/// <summary>
/// Unit tests for ConsoleContext.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConsoleContextTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Act
        var context = new ConsoleContext();

        // Assert
        context.SessionStart.Should().Be(default);
        context.Username.Should().BeEmpty();
        context.ClientAddress.Should().BeEmpty();
        context.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        // Arrange
        var context = new ConsoleContext();
        var sessionStart = DateTimeOffset.UtcNow;
        var username = "admin";
        var clientAddress = "192.168.1.100";
        var config = new TelnetConfiguration { Enabled = true };

        // Act
        context.SessionStart = sessionStart;
        context.Username = username;
        context.ClientAddress = clientAddress;
        context.Configuration = config;

        // Assert
        context.SessionStart.Should().Be(sessionStart);
        context.Username.Should().Be(username);
        context.ClientAddress.Should().Be(clientAddress);
        context.Configuration.Should().BeSameAs(config);
        context.Configuration.Enabled.Should().BeTrue();
    }
}
