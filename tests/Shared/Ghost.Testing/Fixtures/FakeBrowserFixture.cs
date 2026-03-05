using Ghost.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Testing.Fixtures;

/// <summary>
/// Test fixture that provides a fake browser session without requiring real browser automation.
/// Use this for E2E tests that don't need actual external services.
/// </summary>
public class FakeBrowserFixture : IAsyncLifetime
{
    public IServiceProvider Services { get; private set; } = null!;
    public FakeBrowserSession BrowserSession { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        
        // Register fake browser session
        BrowserSession = new FakeBrowserSession();
        services.AddSingleton<IBrowserSession>(BrowserSession);
        services.AddSingleton<FakeBrowserSession>(BrowserSession);
        
        // Register other common dependencies
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLogging();
        
        Services = services.BuildServiceProvider();
        
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
        await Task.CompletedTask;
    }
}
