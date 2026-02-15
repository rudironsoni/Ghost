using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Ghost.Sdk.Console;

/// <summary>
/// Help command that displays available commands.
/// </summary>
internal sealed class HelpCommand : IConsoleCommand
{
    private readonly ConcurrentDictionary<string, IConsoleCommand> _commands;

    public HelpCommand(ConcurrentDictionary<string, IConsoleCommand> commands)
    {
        _commands = commands;
    }

    public string Name => "help";
    public string Description => "Display available commands";
    public string Usage => "help [command]";

    public Task<string> ExecuteAsync(string[] args, ConsoleContext context, CancellationToken ct = default)
    {
        if (args.Length > 0)
        {
            // Show help for specific command
            string commandName = args[0];
            if (_commands.TryGetValue(commandName, out IConsoleCommand? command))
            {
                var sb = new StringBuilder();
                sb.AppendLine(CultureInfo.InvariantCulture, $"Command: {command.Name}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Description: {command.Description}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Usage: {command.Usage}");
                return Task.FromResult(sb.ToString());
            }

            return Task.FromResult($"Unknown command: {commandName}");
        }

        // Show all commands
        var output = new StringBuilder();
        output.AppendLine("Available commands:");
        output.AppendLine();

        foreach (IConsoleCommand? command in _commands.Values.OrderBy(c => c.Name))
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {command.Name,-15} - {command.Description}");
        }

        output.AppendLine();
        output.AppendLine("Type 'help <command>' for detailed usage information.");

        return Task.FromResult(output.ToString());
    }
}

/// <summary>
/// Exit command to close the session.
/// </summary>
internal sealed class ExitCommand : IConsoleCommand
{
    public string Name => "exit";
    public string Description => "Exit the console session";
    public string Usage => "exit";

    public Task<string> ExecuteAsync(string[] args, ConsoleContext context, CancellationToken ct = default)
    {
        return Task.FromResult("Goodbye!");
    }
}

/// <summary>
/// Status command to display console status.
/// </summary>
internal sealed class StatusCommand : IConsoleCommand
{
    public string Name => "status";
    public string Description => "Display console status";
    public string Usage => "status";

    public Task<string> ExecuteAsync(string[] args, ConsoleContext context, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Console Status:");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Session Start: {context.SessionStart:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Session Duration: {DateTimeOffset.UtcNow - context.SessionStart:hh\\:mm\\:ss}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Client Address: {context.ClientAddress}");
        sb.AppendLine("  Commands Enabled: help, exit, status, sessions, history");

        return Task.FromResult(sb.ToString());
    }
}

/// <summary>
/// Sessions command to display active sessions.
/// </summary>
internal sealed class SessionsCommand : IConsoleCommand
{
    private readonly ConcurrentDictionary<string, TelnetSession> _sessions;

    public SessionsCommand(ConcurrentDictionary<string, TelnetSession> sessions)
    {
        _sessions = sessions;
    }

    public string Name => "sessions";
    public string Description => "Display active console sessions";
    public string Usage => "sessions";

    public Task<string> ExecuteAsync(string[] args, ConsoleContext context, CancellationToken ct = default)
    {
        if (_sessions.IsEmpty)
        {
            return Task.FromResult("No active sessions.");
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Active Sessions ({_sessions.Count}):");
        sb.AppendLine();

        foreach (TelnetSession? session in _sessions.Values.OrderBy(s => s.ConnectedAt))
        {
            TimeSpan duration = DateTimeOffset.UtcNow - session.ConnectedAt;
            TimeSpan idle = DateTimeOffset.UtcNow - session.LastActivity;

            sb.AppendLine(CultureInfo.InvariantCulture, $"Session ID: {session.SessionId}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Client: {session.ClientAddress}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Connected: {session.ConnectedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Duration: {duration:hh\\:mm\\:ss}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Idle: {idle:hh\\:mm\\:ss}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Authenticated: {session.IsAuthenticated}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Commands: {session.CommandHistory.Count}");
            sb.AppendLine();
        }

        return Task.FromResult(sb.ToString());
    }
}

/// <summary>
/// History command to display command history.
/// </summary>
internal sealed class HistoryCommand : IConsoleCommand
{
    public string Name => "history";
    public string Description => "Display command history for this session";
    public string Usage => "history [count]";

    public Task<string> ExecuteAsync(string[] args, ConsoleContext context, CancellationToken ct = default)
    {
        // Note: We need to get the history from the session, but we don't have access to it here.
        // This is a simplified implementation.
        return Task.FromResult("Command history is maintained per session. Use 'sessions' to see session details.");
    }
}
