using Ghost.Kernel;
using Ghost.Testing.Fakes;

namespace Ghost.Testing.Fixtures;

/// <summary>
/// xUnit fixture for browser-based tests. Provides a shared browser instance
/// that can be reused across tests in the same collection.
/// </summary>
public class BrowserFixture : IAsyncDisposable
{
    private readonly StubGhostKernel _kernel;

    public BrowserFixture()
    {
        _kernel = new StubGhostKernel();
    }

    public IGhostKernel Kernel => _kernel;

    public async Task<IBrowserSession> CreateSessionAsync(SessionOptions? options = null)
    {
        return await _kernel.NewSessionAsync(options);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _kernel.DisposeAsync();
    }
}
