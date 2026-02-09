namespace Ghost.Sdk.Console;

/// <summary>
/// Interface for telnet console commands.
/// Custom commands can be registered to extend console functionality.
/// </summary>
public interface IConsoleCommand
{
    /// <summary>
    /// Gets the command name (used to invoke the command).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the command description (shown in help text).
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the usage information (shown in help text).
    /// </summary>
    string Usage { get; }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="args">Command arguments.</param>
    /// <param name="context">Console context with access to spider state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command output string.</returns>
    Task<string> ExecuteAsync(string[] args, ConsoleContext context, CancellationToken ct = default);
}
