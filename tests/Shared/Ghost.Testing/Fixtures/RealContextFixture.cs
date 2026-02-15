using Ghost.Kernel;
using Xunit;

namespace Ghost.Testing.Fixtures;

/// <summary>
/// xUnit class fixture for integration tests. Provides a fresh browser context
/// per test class, ensuring isolated cookies/storage between test classes.
/// Use with IClassFixture&lt;RealContextFixture&gt;.
/// </summary>
public sealed class RealContextFixture : IAsyncLifetime
{
    private readonly RealBrowserFixture _browserFixture;
    private IBrowserSession? _session;

    public RealContextFixture(RealBrowserFixture browserFixture)
    {
        _browserFixture = browserFixture;
    }

    public IBrowserSession Session => _session ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        // Create a fresh session for this test class
        _session = await _browserFixture.CreateSessionAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_session != null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
