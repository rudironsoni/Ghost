using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Console;

/// <summary>
/// Telnet console implementation for runtime debugging and control.
/// Provides a telnet interface for inspecting and controlling running spiders.
/// </summary>
public sealed partial class TelnetConsole : ITelnetConsole, IAsyncDisposable
{
    private readonly TelnetConfiguration _config;
    private readonly ILogger<TelnetConsole> _logger;
    private readonly ConcurrentDictionary<string, IConsoleCommand> _commands;
    private readonly ConcurrentDictionary<string, TelnetSession> _sessions;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;

    // LoggerMessage source generators
    [LoggerMessage(LogLevel.Debug, "Registered console command: {CommandName}")]
    partial void LogCommandRegistered(string commandName);

    [LoggerMessage(LogLevel.Information, "Telnet console is disabled")]
    partial void LogConsoleDisabled();

    [LoggerMessage(LogLevel.Warning, "Telnet console is already running")]
    partial void LogConsoleAlreadyRunning();

    [LoggerMessage(LogLevel.Information, "Telnet console started on {BindAddress}:{Port}")]
    partial void LogConsoleStarted(string bindAddress, int port);

    [LoggerMessage(LogLevel.Information, "Stopping telnet console...")]
    partial void LogConsoleStopping();

    [LoggerMessage(LogLevel.Information, "Telnet console stopped")]
    partial void LogConsoleStopped();

    [LoggerMessage(LogLevel.Warning, "Max connections reached, rejecting client")]
    partial void LogMaxConnectionsReached();

    [LoggerMessage(LogLevel.Warning, "Connection rejected from non-whitelisted IP: {ClientIp}")]
    partial void LogConnectionRejected(string clientIp);

    [LoggerMessage(LogLevel.Information, "Accepted connection from {ClientIp}")]
    partial void LogConnectionAccepted(string clientIp);

    [LoggerMessage(LogLevel.Error, "Error accepting client connection")]
    partial void LogAcceptError(Exception ex);

    [LoggerMessage(LogLevel.Information, "Command executed: {Command} (Session: {SessionId}, User: {User})")]
    partial void LogCommandExecuted(string command, string sessionId, string user);

    [LoggerMessage(LogLevel.Error, "Error processing command: {Command}")]
    partial void LogCommandError(Exception ex, string command);

    [LoggerMessage(LogLevel.Error, "Error handling client session {SessionId}")]
    partial void LogSessionError(Exception ex, string sessionId);

    [LoggerMessage(LogLevel.Information, "Session {SessionId} closed (Duration: {Duration})")]
    partial void LogSessionClosed(string sessionId, TimeSpan duration);

    [LoggerMessage(LogLevel.Warning, "Authentication failed for session {SessionId} from {ClientAddress}")]
    partial void LogAuthenticationFailed(string sessionId, string clientAddress);

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnetConsole"/> class.
    /// </summary>
    /// <param name="config">Telnet configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public TelnetConsole(TelnetConfiguration config, ILogger<TelnetConsole> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config;
        _logger = logger;
        _commands = new ConcurrentDictionary<string, IConsoleCommand>(StringComparer.OrdinalIgnoreCase);
        _sessions = new ConcurrentDictionary<string, TelnetSession>();

        RegisterBuiltInCommands();
    }

    /// <summary>
    /// Registers a custom command.
    /// </summary>
    /// <param name="command">The command to register.</param>
    public void RegisterCommand(IConsoleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands[command.Name] = command;
        LogCommandRegistered(command.Name);
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            LogConsoleDisabled();
            return;
        }

        if (_listener != null)
        {
            LogConsoleAlreadyRunning();
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var bindAddress = IPAddress.Parse(_config.BindAddress);
        _listener = new TcpListener(bindAddress, _config.Port);
        _listener.Start();

        LogConsoleStarted(_config.BindAddress, _config.Port);

        _acceptTask = AcceptClientsAsync(_cts.Token);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_listener == null)
        {
            return;
        }

        LogConsoleStopping();

        _cts?.Cancel();
        _listener?.Stop();

        // Wait for accept task to complete
        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        _sessions.Clear();
        _listener = null;

        LogConsoleStopped();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts?.Dispose();
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);

                // Check connection limit
                if (_sessions.Count >= _config.MaxConnections)
                {
                    LogMaxConnectionsReached();
                    client.Close();
                    continue;
                }

                // Check IP whitelist
                var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
                string clientIp = endpoint?.Address.ToString() ?? "unknown";

                if (!IsIpAllowed(clientIp))
                {
                    LogConnectionRejected(clientIp);
                    client.Close();
                    continue;
                }

                LogConnectionAccepted(clientIp);

                // Handle client in background
                _ = Task.Run(async () => await HandleClientAsync(client, ct).ConfigureAwait(false), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogAcceptError(ex);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var session = new TelnetSession
        {
            ConnectedAt = DateTimeOffset.UtcNow,
            LastActivity = DateTimeOffset.UtcNow,
            ClientAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "unknown"
        };

        _sessions[session.SessionId] = session;

        try
        {
            using NetworkStream stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Send welcome banner
            await writer.WriteLineAsync("Ghost Spider Telnet Console").ConfigureAwait(false);
            await writer.WriteLineAsync($"Session ID: {session.SessionId}").ConfigureAwait(false);
            await writer.WriteLineAsync("Type 'help' for available commands").ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);

            // Authenticate if credentials are configured
            if (!string.IsNullOrEmpty(_config.Username) || !string.IsNullOrEmpty(_config.Password))
            {
                if (!await AuthenticateAsync(reader, writer, session, ct).ConfigureAwait(false))
                {
                    await writer.WriteLineAsync("Authentication failed. Disconnecting...").ConfigureAwait(false);
                    return;
                }
            }
            else
            {
                session.IsAuthenticated = true;
            }

            // Command loop
            while (!ct.IsCancellationRequested)
            {
                // Check session timeout
                if (DateTimeOffset.UtcNow - session.LastActivity > _config.SessionTimeout)
                {
                    await writer.WriteLineAsync("Session timeout. Disconnecting...").ConfigureAwait(false);
                    break;
                }

                await writer.WriteAsync("> ").ConfigureAwait(false);
                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                if (line == null)
                {
                    break; // Client disconnected
                }

                session.LastActivity = DateTimeOffset.UtcNow;

                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                // Add to history
                if (_config.EnableCommandHistory)
                {
                    session.CommandHistory.Add(trimmedLine);
                    if (session.CommandHistory.Count > _config.MaxHistorySize)
                    {
                        session.CommandHistory.RemoveAt(0);
                    }
                }

                // Log command if enabled
                if (_config.LogCommands)
                {
                    LogCommandExecuted(trimmedLine, session.SessionId, session.ClientAddress);
                }

                // Process command
                try
                {
                    string response = await ProcessCommandAsync(trimmedLine, session, ct).ConfigureAwait(false);
                    await writer.WriteLineAsync(response).ConfigureAwait(false);

                    // Check for exit command
                    if (trimmedLine.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                        trimmedLine.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    LogCommandError(ex, trimmedLine);
                    await writer.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            LogSessionError(ex, session.SessionId);
        }
        finally
        {
            _sessions.TryRemove(session.SessionId, out _);
            client.Close();
            LogSessionClosed(session.SessionId, DateTimeOffset.UtcNow - session.ConnectedAt);
        }
    }

    private async Task<bool> AuthenticateAsync(
        StreamReader reader,
        StreamWriter writer,
        TelnetSession session,
        CancellationToken ct)
    {
        const int MaxAttempts = 3;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await writer.WriteAsync("Username: ").ConfigureAwait(false);
            string? username = await reader.ReadLineAsync(ct).ConfigureAwait(false);

            await writer.WriteAsync("Password: ").ConfigureAwait(false);
            string? password = await reader.ReadLineAsync(ct).ConfigureAwait(false);

            if (IsAuthenticationValid(username, password))
            {
                session.IsAuthenticated = true;
                await writer.WriteLineAsync("Authentication successful").ConfigureAwait(false);
                await writer.WriteLineAsync().ConfigureAwait(false);
                return true;
            }

            if (attempt < MaxAttempts)
            {
                await writer.WriteLineAsync($"Invalid credentials. {MaxAttempts - attempt} attempts remaining.").ConfigureAwait(false);
            }
        }

        LogAuthenticationFailed(session.SessionId, session.ClientAddress);

        return false;
    }

    private bool IsAuthenticationValid(string? username, string? password)
    {
        if (string.IsNullOrEmpty(_config.Username) && string.IsNullOrEmpty(_config.Password))
        {
            return true; // No authentication required
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        bool usernameMatch = string.IsNullOrEmpty(_config.Username) ||
                            CryptographicOperations.FixedTimeEquals(
                                Encoding.UTF8.GetBytes(username),
                                Encoding.UTF8.GetBytes(_config.Username));

        bool passwordMatch = string.IsNullOrEmpty(_config.Password) ||
                            CryptographicOperations.FixedTimeEquals(
                                Encoding.UTF8.GetBytes(password),
                                Encoding.UTF8.GetBytes(_config.Password));

        return usernameMatch && passwordMatch;
    }

    private bool IsIpAllowed(string clientIp)
    {
        // If no whitelist is configured, allow all IPs
        if (_config.AllowedIps.Count == 0)
        {
            return true;
        }

        // Check if client IP is in the whitelist
        foreach (string allowedIp in _config.AllowedIps)
        {
            if (allowedIp.Equals(clientIp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // TODO: Add CIDR range support in a future enhancement
        }

        return false;
    }

    private async Task<string> ProcessCommandAsync(
        string commandLine,
        TelnetSession session,
        CancellationToken ct)
    {
        string[] parts = ParseCommandLine(commandLine);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        string commandName = parts[0];
        string[] args = parts.Skip(1).ToArray();

        if (_commands.TryGetValue(commandName, out IConsoleCommand? command))
        {
            var context = new ConsoleContext
            {
                SessionStart = session.ConnectedAt,
                Username = session.ClientAddress,
                ClientAddress = session.ClientAddress,
                Configuration = _config
            };

            return await command.ExecuteAsync(args, context, ct).ConfigureAwait(false);
        }

        return $"Unknown command: {commandName}. Type 'help' for available commands.";
    }

    private static string[] ParseCommandLine(string commandLine)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts.ToArray();
    }

    private void RegisterBuiltInCommands()
    {
        RegisterCommand(new HelpCommand(_commands));
        RegisterCommand(new ExitCommand());
        RegisterCommand(new StatusCommand());
        RegisterCommand(new SessionsCommand(_sessions));
        RegisterCommand(new HistoryCommand());
    }
}
