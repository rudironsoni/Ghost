using System.Collections.Concurrent;
using FluentAssertions;
using Ghost.Sdk.Console;
using Xunit;

namespace Ghost.Sdk.Tests.Console;

/// <summary>
/// Unit tests for built-in console commands.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BuiltInCommandsTests
{
    [Fact]
    public async Task HelpCommand_WithNoArgs_ReturnsAllCommands()
    {
        // Arrange
        var commands = new ConcurrentDictionary<string, IConsoleCommand>(StringComparer.OrdinalIgnoreCase);
        var helpCommand = new HelpCommand(commands);
        commands["help"] = helpCommand;
        commands["exit"] = new ExitCommand();

        var context = new ConsoleContext();

        // Act
        var result = await helpCommand.ExecuteAsync(Array.Empty<string>(), context);

        // Assert
        result.Should().Contain("Available commands:");
        result.Should().Contain("help");
        result.Should().Contain("exit");
    }

    [Fact]
    public async Task HelpCommand_WithValidCommand_ReturnsCommandHelp()
    {
        // Arrange
        var commands = new ConcurrentDictionary<string, IConsoleCommand>(StringComparer.OrdinalIgnoreCase);
        var helpCommand = new HelpCommand(commands);
        var exitCommand = new ExitCommand();
        commands["help"] = helpCommand;
        commands["exit"] = exitCommand;

        var context = new ConsoleContext();

        // Act
        var result = await helpCommand.ExecuteAsync(new[] { "exit" }, context);

        // Assert
        result.Should().Contain("Command: exit");
        result.Should().Contain("Description:");
        result.Should().Contain("Usage:");
    }

    [Fact]
    public async Task HelpCommand_WithInvalidCommand_ReturnsError()
    {
        // Arrange
        var commands = new ConcurrentDictionary<string, IConsoleCommand>(StringComparer.OrdinalIgnoreCase);
        var helpCommand = new HelpCommand(commands);
        var context = new ConsoleContext();

        // Act
        var result = await helpCommand.ExecuteAsync(new[] { "invalid" }, context);

        // Assert
        result.Should().Contain("Unknown command: invalid");
    }

    [Fact]
    public async Task ExitCommand_ReturnsGoodbyeMessage()
    {
        // Arrange
        var command = new ExitCommand();
        var context = new ConsoleContext();

        // Act
        var result = await command.ExecuteAsync(Array.Empty<string>(), context);

        // Assert
        result.Should().Be("Goodbye!");
    }

    [Fact]
    public void ExitCommand_HasCorrectMetadata()
    {
        // Arrange
        var command = new ExitCommand();

        // Assert
        command.Name.Should().Be("exit");
        command.Description.Should().NotBeEmpty();
        command.Usage.Should().Be("exit");
    }

    [Fact]
    public async Task StatusCommand_ReturnsSessionInfo()
    {
        // Arrange
        var command = new StatusCommand();
        var context = new ConsoleContext
        {
            SessionStart = DateTimeOffset.UtcNow.AddMinutes(-5),
            ClientAddress = "192.168.1.100"
        };

        // Act
        var result = await command.ExecuteAsync(Array.Empty<string>(), context);

        // Assert
        result.Should().Contain("Console Status:");
        result.Should().Contain("Session Start:");
        result.Should().Contain("Session Duration:");
        result.Should().Contain("Client Address: 192.168.1.100");
    }

    [Fact]
    public async Task SessionsCommand_WithNoSessions_ReturnsEmptyMessage()
    {
        // Arrange
        var sessions = new ConcurrentDictionary<string, TelnetSession>();
        var command = new SessionsCommand(sessions);
        var context = new ConsoleContext();

        // Act
        var result = await command.ExecuteAsync(Array.Empty<string>(), context);

        // Assert
        result.Should().Be("No active sessions.");
    }

    [Fact]
    public async Task SessionsCommand_WithActiveSessions_ReturnsSessionList()
    {
        // Arrange
        var sessions = new ConcurrentDictionary<string, TelnetSession>();
        var session1 = new TelnetSession
        {
            SessionId = "session-1",
            ClientAddress = "192.168.1.100",
            ConnectedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastActivity = DateTimeOffset.UtcNow.AddMinutes(-2),
            IsAuthenticated = true
        };
        session1.CommandHistory.Add("help");
        session1.CommandHistory.Add("status");

        sessions[session1.SessionId] = session1;

        var command = new SessionsCommand(sessions);
        var context = new ConsoleContext();

        // Act
        var result = await command.ExecuteAsync(Array.Empty<string>(), context);

        // Assert
        result.Should().Contain("Active Sessions (1):");
        result.Should().Contain("Session ID: session-1");
        result.Should().Contain("Client: 192.168.1.100");
        result.Should().Contain("Authenticated: True");
        result.Should().Contain("Commands: 2");
    }

    [Fact]
    public async Task HistoryCommand_ReturnsMessage()
    {
        // Arrange
        var command = new HistoryCommand();
        var context = new ConsoleContext();

        // Act
        var result = await command.ExecuteAsync(Array.Empty<string>(), context);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("history");
    }
}
