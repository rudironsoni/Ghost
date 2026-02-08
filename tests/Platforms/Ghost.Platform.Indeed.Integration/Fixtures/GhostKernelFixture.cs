using Ghost.Core;
using Ghost.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Indeed.Integration.Fixtures;

/// <summary>
/// Test fixture that provides a GhostKernel instance for Indeed integration tests.
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

        // Build service provider with Indeed platform
        var services = new ServiceCollection();
        services.AddSingleton(Session);
        services.AddSingleton<IBrowserSession>(Session);
        services.AddLogging();

        // Register Indeed services manually
        var indeedOptions = new Ghost.Platform.Indeed.IndeedOptions
        {
            ApiKey = "test-api-key-for-integration-tests" // Required by IndeedConstants.GetHeaders
        };
        var proxyProvider = Ghost.Proxy.StaticProxyProvider.Empty;

        services.AddSingleton(indeedOptions);
        services.AddSingleton<Ghost.Abstractions.IProxyProvider>(proxyProvider);
        services.AddSingleton<Ghost.Platform.Indeed.Internal.IndeedApiClient>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Indeed.Internal.IndeedApiClient>>();
            return new Ghost.Platform.Indeed.Internal.IndeedApiClient(proxyProvider, indeedOptions, logger);
        });
        services.AddScoped<Ghost.Platform.Indeed.IndeedJobClient>();

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
