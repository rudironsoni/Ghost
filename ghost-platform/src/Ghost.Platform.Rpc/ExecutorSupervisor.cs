using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Rpc;

/// <summary>
/// Supervisor for managing out-of-process executor lifecycle.
/// </summary>
public sealed class ExecutorSupervisor : IExecutorSupervisor
{
    private readonly SupervisionOptions _options;
    private readonly ILogger<ExecutorSupervisor> _logger;
    private readonly object _lock = new();
    private Process? _process;
    private IExecutorClient? _client;
    private int _restartCount;
    private DateTimeOffset? _lastRestartTimeUtc = null;
    private CancellationTokenSource? _shutdownCts;
    private Task? _processMonitorTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorSupervisor"/> class.
    /// </summary>
    /// <param name="options">Supervision options.</param>
    /// <param name="logger">Logger instance.</param>
    public ExecutorSupervisor(
        IOptions<SupervisionOptions> options,
        ILogger<ExecutorSupervisor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _process != null && !_process.HasExited;
            }
        }
    }

    /// <inheritdoc/>
    public int? ProcessId
    {
        get
        {
            lock (_lock)
            {
                return _process?.Id;
            }
        }
    }

    /// <inheritdoc/>
    public int RestartCount
    {
        get
        {
            lock (_lock)
            {
                return _restartCount;
            }
        }
    }

    /// <inheritdoc/>
    public DateTimeOffset? LastRestartTimeUtc
    {
        get
        {
            lock (_lock)
            {
                return _lastRestartTimeUtc;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<ExecutorExitedEventArgs>? ExecutorExited;

    /// <inheritdoc/>
    public event EventHandler<ExecutorRestartedEventArgs>? ExecutorRestarted;

    /// <inheritdoc/>
    public async Task<IExecutorClient> StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_process != null && !_process.HasExited)
            {
                throw new InvalidOperationException("Executor is already running.");
            }

            _shutdownCts = new CancellationTokenSource();
        }

        try
        {
            _logger.LogInformation("Starting executor process: {ExecutorPath}", _options.ExecutorPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = _options.ExecutorPath,
                Arguments = _options.ExecutorArguments,
                WorkingDirectory = _options.WorkingDirectory,
                CreateNoWindow = _options.CreateNoWindow,
                UseShellExecute = _options.UseShellExecute,
                RedirectStandardOutput = _options.RedirectStandardOutput,
                RedirectStandardError = _options.RedirectStandardError,
                RedirectStandardInput = _options.RedirectStandardInput
            };

            // Set environment variables
            foreach (var kvp in _options.EnvironmentVariables)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.Exited += OnProcessExited;

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start executor process: {_options.ExecutorPath}");
            }

            lock (_lock)
            {
                _process = process;
                _restartCount = 0;
                _lastRestartTimeUtc = null;
            }

            _logger.LogInformation("Executor process started with PID: {ProcessId}", process.Id);

            // Start monitoring the process
            _processMonitorTask = Task.Run(() => MonitorProcessAsync(_shutdownCts.Token));

            // Create client (placeholder - actual implementation would depend on transport)
            _client = CreateExecutorClient(process);

            // Wait for startup timeout
            await Task.Delay(_options.StartupTimeout, cancellationToken);

            return _client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start executor process");
            await CleanupAsync();
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? processToStop;
        lock (_lock)
        {
            processToStop = _process;
            _shutdownCts?.Cancel();
        }

        if (processToStop == null || processToStop.HasExited)
        {
            _logger.LogInformation("Executor process is not running");
            return;
        }

        _logger.LogInformation("Stopping executor process: {ProcessId}", processToStop.Id);

        try
        {
            // Try graceful shutdown first
            using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            shutdownCts.CancelAfter(_options.ShutdownTimeout);
            var shutdownTask = processToStop.WaitForExitAsync(shutdownCts.Token);
            var timeoutTask = Task.Delay(_options.ShutdownTimeout, cancellationToken);

            var completedTask = await Task.WhenAny(shutdownTask, timeoutTask);

            if (completedTask == timeoutTask && _options.KillOnShutdownTimeout)
            {
                _logger.LogWarning("Executor did not shut down gracefully, killing process");
                processToStop.Kill(entireProcessTree: true);
                await processToStop.WaitForExitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping executor process");
            throw;
        }
        finally
        {
            await CleanupAsync();
        }
    }

    /// <inheritdoc/>
    public async Task<IExecutorClient> RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Restarting executor process");

        var previousProcessId = ProcessId;

        await StopAsync(cancellationToken);

        lock (_lock)
        {
            _restartCount++;
            _lastRestartTimeUtc = DateTimeOffset.UtcNow;
        }

        if (_restartCount > _options.MaxRestartAttempts)
        {
            throw new InvalidOperationException(
                $"Maximum restart attempts ({_options.MaxRestartAttempts}) exceeded");
        }

        await Task.Delay(_options.RestartDelay, cancellationToken);

        var client = await StartAsync(cancellationToken);

        var newProcessId = ProcessId;
        if (previousProcessId.HasValue && newProcessId.HasValue)
        {
            ExecutorRestarted?.Invoke(this, new ExecutorRestartedEventArgs
            {
                PreviousProcessId = previousProcessId.Value,
                NewProcessId = newProcessId.Value,
                AttemptNumber = _restartCount
            });
        }

        return client;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _shutdownCts?.Dispose();
        _processMonitorTask?.Dispose();
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (sender is not Process process)
            return;

        var exitCode = process.ExitCode;
        var wasUnexpected = _shutdownCts?.IsCancellationRequested != true;

        _logger.LogInformation(
            "Executor process exited: {ProcessId}, ExitCode: {ExitCode}, Unexpected: {WasUnexpected}",
            process.Id,
            exitCode,
            wasUnexpected);

        ExecutorExited?.Invoke(this, new ExecutorExitedEventArgs
        {
            ExitCode = exitCode,
            WasUnexpected = wasUnexpected,
            Reason = wasUnexpected ? "Process exited unexpectedly" : null
        });

        // Attempt restart if configured and unexpected
        if (wasUnexpected && _options.RestartOnCrash && _restartCount < _options.MaxRestartAttempts)
        {
            _logger.LogInformation("Attempting to restart executor after unexpected exit");
            _ = Task.Run(async () =>
            {
                try
                {
                    await RestartAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restart executor after unexpected exit");
                }
            });
        }
    }

    private async Task MonitorProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Process? process;
                lock (_lock)
                {
                    process = _process;
                }

                if (process == null || process.HasExited)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    private async Task CleanupAsync()
    {
        Process? process;
        lock (_lock)
        {
            process = _process;
            _process = null;
            _client = null;
        }

        if (process != null)
        {
            process.Exited -= OnProcessExited;
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to kill process during cleanup");
                }
            }
            process.Dispose();
        }

        var monitorTask = _processMonitorTask;
        _processMonitorTask = null;

        if (monitorTask != null)
        {
            try
            {
                await monitorTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    private IExecutorClient CreateExecutorClient(Process process)
    {
        // Placeholder implementation
        // In a real implementation, this would create a concrete client
        // that communicates with the process via stdin/stdout, named pipes, etc.
        return new PlaceholderExecutorClient(process, _logger);
    }

    /// <summary>
    /// Placeholder executor client for demonstration.
    /// </summary>
    private sealed class PlaceholderExecutorClient : IExecutorClient
    {
        private readonly Process _process;
        private readonly ILogger _logger;

        public PlaceholderExecutorClient(Process process, ILogger logger)
        {
            _process = process;
            _logger = logger;
        }

        public bool IsConnected => !_process.HasExited;
        public string? ExecutorId => _process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public Task<HandshakeResponse> HandshakeAsync(
            HandshakeRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Handshake requested (placeholder)");
            return Task.FromResult(new HandshakeResponse
            {
                ProtocolVersion = ProtocolVersion.Current,
                ExecutorId = ExecutorId ?? "unknown",
                Success = true
            });
        }

        public async IAsyncEnumerable<ExecutorMessage> StreamMessagesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Placeholder - in real implementation, this would read from process stdout
            await Task.CompletedTask;
            yield break;
        }

        public Task<StartJobResponse> StartJobAsync(
            StartJobRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Start job requested (placeholder)");
            return Task.FromResult(new StartJobResponse
            {
                RunId = Guid.NewGuid().ToString(),
                Success = true
            });
        }

        public Task<StopJobResponse> StopJobAsync(
            StopJobRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Stop job requested (placeholder)");
            return Task.FromResult(new StopJobResponse
            {
                RunId = request.RunId,
                Success = true
            });
        }

        public Task<ShutdownResponse> ShutdownAsync(
            ShutdownRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Shutdown requested (placeholder)");
            return Task.FromResult(new ShutdownResponse
            {
                Acknowledged = true
            });
        }

        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask;
        }
    }
}
