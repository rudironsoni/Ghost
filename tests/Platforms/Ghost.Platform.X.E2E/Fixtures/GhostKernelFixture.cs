using Ghost.Core;
using Ghost.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.X.E2E.Fixtures;

/// <summary>
/// Test fixture that provides a GhostKernel instance for E2E tests.
/// </summary>
public sealed class GhostKernelFixture : IAsyncLifetime
{
    public GhostKernel Kernel { get; private set; } = null!;
    public IBrowserSession Session { get; private set; } = null!;
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var options = new KernelOptions
        {
            EnableStealth = true,
            Headless = true
        };

        Kernel = await GhostKernel.CreateAsync(options);

        // Create a session for testing
        Session = await Kernel.NewSessionAsync();

        // Build service provider with X platform
        var services = new ServiceCollection();
        services.AddSingleton(Session);
        services.AddSingleton<IBrowserSession>(Session);
        services.AddLogging();
        services.AddXPlatform();

        ServiceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (Session != null)
        {
            await Session.DisposeAsync();
        }

        if (Kernel != null)
        {
            await Kernel.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a new browser session for testing.
    /// </summary>
    public async Task<IBrowserSession> CreateSessionAsync()
    {
        return await Kernel.NewSessionAsync();
    }
}
