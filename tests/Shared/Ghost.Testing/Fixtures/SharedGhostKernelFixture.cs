using Ghost.Core;
using Ghost.Kernel.Configuration;
using Xunit;

namespace Ghost.Testing.Fixtures;

/// <summary>
/// Shared GhostKernel fixture for integration tests across multiple test classes.
/// Uses reference counting to manage lifecycle - kernel is created on first use
/// and disposed when last test class completes. This reduces browser instances
/// from O(n) to O(1) where n = number of test classes.
/// </summary>
public sealed class SharedGhostKernelFixture : IAsyncLifetime, IDisposable
{
    private GhostKernel? _kernel;
    private int _referenceCount;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly KernelOptions _options;
    private bool _disposed;

    public SharedGhostKernelFixture()
    {
        _options = new KernelOptions
        {
            EnableStealth = true,
            Headless = true
        };
    }

    /// <summary>
    /// Gets the shared kernel instance. Thread-safe.
    /// </summary>
    public IGhostKernel Kernel => _kernel ?? throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Gets the kernel as a concrete GhostKernel type.
    /// Use this when you need the concrete type for DI registration.
    /// </summary>
    public GhostKernel ConcreteKernel => _kernel ?? throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Initializes the shared kernel on first test class usage.
    /// Subsequent calls increment reference count without re-initializing.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Interlocked.Increment(ref _referenceCount) == 1)
            {
                // First test class using this fixture - create the kernel
                _kernel = await GhostKernel.CreateAsync(_options).ConfigureAwait(false);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Decrements reference count and disposes kernel when last test class completes.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                // Last test class done - dispose the kernel
                if (_kernel != null)
                {
                    await _kernel.DisposeAsync().ConfigureAwait(false);
                    _kernel = null;
                }
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Creates a new isolated browser session from the shared kernel.
    /// Each session has its own cookies, storage, and state.
    /// </summary>
    public async Task<IBrowserSession> CreateSessionAsync(SessionOptions? options = null)
    {
        if (_kernel == null)
        {
            throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");
        }

        return await _kernel.NewSessionAsync(options).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes the semaphore lock used for synchronization.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _initLock.Dispose();
            _disposed = true;
        }
    }
}
