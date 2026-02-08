using Ghost.Core;
using Ghost.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Glassdoor.Integration.Fixtures;

/// <summary>
/// Test fixture that provides a GhostKernel instance for Glassdoor integration tests.
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

        // Build service provider with Glassdoor platform
        var services = new ServiceCollection();
        services.AddSingleton(Session);
        services.AddSingleton<IBrowserSession>(Session);
        services.AddLogging();

        // Register Glassdoor services manually
        var glassdoorOptions = Microsoft.Extensions.Options.Options.Create(new Ghost.Platform.Glassdoor.GlassdoorOptions());
        var proxyProvider = Ghost.Proxy.StaticProxyProvider.Empty;

        services.AddSingleton(Kernel);
        services.AddSingleton(glassdoorOptions);
        services.AddSingleton<Ghost.Abstractions.IProxyProvider>(proxyProvider);
        services.AddSingleton<System.Net.Http.HttpClient>();
        services.AddSingleton<Ghost.Platform.Glassdoor.Internal.GlassdoorApiClient>();
        services.AddSingleton<Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient>>();
            return new Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient(Kernel, glassdoorOptions, logger, proxyProvider);
        });
        services.AddScoped<Ghost.Platform.Glassdoor.GlassdoorJobClient>();

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
