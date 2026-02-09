namespace Ghost.Sdk.Console;

/// <summary>
/// Interface for telnet console debugging server.
/// Provides runtime introspection and control capabilities for Ghost spiders.
/// </summary>
public interface ITelnetConsole
{
    /// <summary>
    /// Starts the telnet console server.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the server.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the telnet console server and closes all client connections.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync(CancellationToken ct = default);
}
