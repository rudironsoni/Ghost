using Xunit;

namespace Ghost.Testing.Reliability;

/// <summary>
/// Base class for tests requiring timeout enforcement and cancellation token propagation.
/// Implements xUnit's <see cref="IAsyncLifetime"/> to manage test lifecycle.
/// </summary>
/// <remarks>
/// This base class provides:
/// <list type="bullet">
/// <item><description>Automatic timeout enforcement (30 seconds default)</description></item>
/// <item><description>CancellationToken for async operations</description></item>
/// <item><description>Guaranteed cleanup via IAsyncLifetime</description></item>
/// </list>
/// Inherit from this class to ensure your tests are protected against hangs.
/// </remarks>
public abstract class ReliabilityTestBase : IAsyncLifetime, IDisposable
{
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Gets a cancellation token that will be cancelled after the test timeout period.
    /// Pass this token to async operations to ensure they respect the test timeout.
    /// </summary>
    protected CancellationToken TestTimeoutToken => _cts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Initializes the test by setting up the timeout cancellation token.
    /// Called automatically by xUnit before the test runs.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        // Set 30-second timeout for test execution
        _cts = new CancellationTokenSource();
        _cts.CancelAfter(TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up test resources by cancelling the timeout token and disposing the CTS.
    /// Called automatically by xUnit after the test completes.
    /// </summary>
    public virtual Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
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
}
