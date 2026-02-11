using Xunit;
using Xunit.Abstractions;

namespace Ghost.Testing.Reliability;

/// <summary>
/// Base class for tests requiring timeout enforcement, cancellation token propagation,
/// and automatic failure diagnostics capture.
/// Implements xUnit's <see cref="IAsyncLifetime"/> to manage test lifecycle.
/// </summary>
/// <remarks>
/// This base class provides:
/// <list type="bullet">
/// <item><description>Automatic timeout enforcement (30 seconds default)</description></item>
/// <item><description>CancellationToken for async operations</description></item>
/// <item><description>Guaranteed cleanup via IAsyncLifetime</description></item>
/// <item><description>Automatic failure diagnostics capture on test failure</description></item>
/// <item><description>Correlation IDs for distributed tracing</description></item>
/// </list>
/// Inherit from this class to ensure your tests are protected against hangs
/// and provide comprehensive diagnostic information on failure.
/// </remarks>
public abstract class ReliabilityTestBase : IAsyncLifetime, IDisposable
{
    private CancellationTokenSource? _cts;
    private FailureDiagnosticsHelper? _diagnostics;
    private bool _disposed;

    /// <summary>
    /// Gets the test output helper for logging.
    /// </summary>
    protected ITestOutputHelper Output { get; }

    /// <summary>
    /// Gets a cancellation token that will be cancelled after the test timeout period.
    /// Pass this token to async operations to ensure they respect the test timeout.
    /// </summary>
    protected CancellationToken TestTimeoutToken => _cts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Gets the failure diagnostics helper for capturing artifacts on test failure.
    /// </summary>
    protected FailureDiagnosticsHelper Diagnostics => _diagnostics ?? throw new InvalidOperationException("Diagnostics not initialized");

    /// <summary>
    /// Initializes a new instance of the <see cref="ReliabilityTestBase"/> class.
    /// </summary>
    /// <param name="output">The test output helper for logging.</param>
    protected ReliabilityTestBase(ITestOutputHelper output)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>
    /// Initializes the test by setting up the timeout cancellation token and diagnostics helper.
    /// Called automatically by xUnit before the test runs.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        // Set 30-second timeout for test execution
        _cts = new CancellationTokenSource();
        _cts.CancelAfter(TimeSpan.FromSeconds(30));

        // Initialize diagnostics helper
        _diagnostics = new FailureDiagnosticsHelper(
            Output,
            scenarioId: GetType().Name,
            fixtureId: GetType().Assembly.GetName().Name ?? "unknown");

        Output.WriteLine($"[Test] Test initialized. Correlation ID: {_diagnostics.CorrelationId}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up test resources by cancelling the timeout token, disposing the CTS,
    /// and cleaning up diagnostics.
    /// Called automatically by xUnit after the test completes.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        if (_diagnostics != null)
        {
            await _diagnostics.DisposeAsync();
        }

        Dispose();
    }

    /// <summary>
    /// Disposes the test resources synchronously.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources used by this test base class.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing && _cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        _disposed = true;
    }

    /// <summary>
    /// Captures failure diagnostics when a test fails.
    /// Call this method in catch blocks to automatically capture all diagnostic artifacts.
    /// </summary>
    /// <param name="page">The browser page (optional).</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>The path to the diagnostics package directory.</returns>
    protected async Task<string> CaptureFailureAsync(Ghost.IPage? page = null, Exception? exception = null)
    {
        if (_diagnostics == null)
        {
            Output.WriteLine("[Warning] Diagnostics helper not initialized");
            return string.Empty;
        }

        return await _diagnostics.CaptureFailureAsync(page, exception);
    }

    /// <summary>
    /// Records a timeline event for the test execution.
    /// </summary>
    /// <param name="eventType">The type of event.</param>
    /// <param name="description">The event description.</param>
    protected void AddTimelineEvent(string eventType, string description)
    {
        _diagnostics?.AddTimelineEvent(eventType, description);
    }
}
