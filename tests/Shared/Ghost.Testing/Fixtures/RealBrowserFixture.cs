using Ghost.Core;
using Ghost.Core.Configuration;
using Xunit;

namespace Ghost.Testing.Fixtures;

/// <summary>
/// xUnit collection fixture for integration tests. Provides a shared real browser instance
/// (GhostKernel) that can be reused across tests in the same collection.
/// Use with [Collection("Browser")] attribute.
/// </summary>
public sealed class RealBrowserFixture : IAsyncLifetime
{
    private GhostKernel? _kernel;

    public IGhostKernel Kernel => _kernel ?? throw new InvalidOperationException("Fixture not initialized");

    /// <summary>
    /// Gets the kernel as a concrete GhostKernel type.
    /// Use this when you need the concrete type for DI registration.
    /// </summary>
    public GhostKernel ConcreteKernel => _kernel ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        var options = new KernelOptions
        {
            EnableStealth = true,
            Headless = true
        };

        _kernel = await GhostKernel.CreateAsync(options);
    }

    public async Task DisposeAsync()
    {
        if (_kernel != null)
        {
            await _kernel.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a new isolated browser session.
    /// Each session has its own cookies, storage, and state.
    /// </summary>
    public async Task<IBrowserSession> CreateSessionAsync(SessionOptions? options = null)
    {
        if (_kernel == null)
        {
            throw new InvalidOperationException("Fixture not initialized");
        }

        return await _kernel.NewSessionAsync(options);
    }
}
