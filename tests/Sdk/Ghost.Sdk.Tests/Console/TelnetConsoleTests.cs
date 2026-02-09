using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ghost.Sdk.Console;
using Xunit;

namespace Ghost.Sdk.Tests.Console;

/// <summary>
/// Unit tests for TelnetConsole.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TelnetConsoleTests
{
    private readonly ILogger<TelnetConsole> _logger = NullLogger<TelnetConsole>.Instance;

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        TelnetConfiguration config = null!;

        // Act
        var act = () => new TelnetConsole(config, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new TelnetConfiguration();
        ILogger<TelnetConsole> logger = null!;

        // Act
        var act = () => new TelnetConsole(config, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNotStartServer()
    {
        // Arrange
        var config = new TelnetConfiguration { Enabled = false };
        var console = new TelnetConsole(config, _logger);

        // Act
        await console.StartAsync();

        // Assert - Should complete without error
        // No actual server should be listening
        await console.StopAsync();
    }

    [Fact]
    public async Task StopAsync_WithoutStart_CompletesSuccessfully()
    {
        // Arrange
        var config = new TelnetConfiguration { Enabled = false };
        var console = new TelnetConsole(config, _logger);

        // Act
        var act = async () => await console.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var config = new TelnetConfiguration
        {
            Enabled = true,
            Port = 0, // Use ephemeral port
            BindAddress = "127.0.0.1"
        };
        var console = new TelnetConsole(config, _logger);

        try
        {
            // Act
            await console.StartAsync();
            var act = async () => await console.StartAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }
        finally
        {
            await console.StopAsync();
        }
    }

    [Fact]
    public void RegisterCommand_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new TelnetConfiguration();
        var console = new TelnetConsole(config, _logger);
        IConsoleCommand command = null!;

        // Act
        var act = () => console.RegisterCommand(command);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("command");
    }

    [Fact]
    public void RegisterCommand_WithValidCommand_SuccessfullyRegisters()
    {
        // Arrange
        var config = new TelnetConfiguration();
        var console = new TelnetConsole(config, _logger);
        var command = new TestCommand();

        // Act
        var act = () => console.RegisterCommand(command);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_StopsServerGracefully()
    {
        // Arrange
        var config = new TelnetConfiguration { Enabled = false };
        var console = new TelnetConsole(config, _logger);

        // Act
        var act = async () => await console.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    private sealed class TestCommand : IConsoleCommand
    {
        public string Name => "test";
        public string Description => "Test command";
        public string Usage => "test";

        public Task<string> ExecuteAsync(string[] args, ConsoleContext context, CancellationToken ct = default)
        {
            return Task.FromResult("test output");
        }
    }
}
