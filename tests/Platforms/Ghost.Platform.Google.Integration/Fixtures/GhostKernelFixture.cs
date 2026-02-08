using Ghost.Core;
using Ghost.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Google.Integration.Fixtures;

/// <summary>
/// Test fixture that provides a GhostKernel instance for Google Jobs integration tests.
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

        // Build service provider with Google Jobs platform
        var services = new ServiceCollection();
        services.AddSingleton(Session);
        services.AddSingleton<IBrowserSession>(Session);
        services.AddLogging();

        // Register Google Jobs services manually
        var googleOptions = new Ghost.Platform.Google.Jobs.GoogleJobsOptions();
        var googleOptionsWrapped = Microsoft.Extensions.Options.Options.Create(googleOptions);
        var httpClient = new System.Net.Http.HttpClient();

        services.AddSingleton(Kernel);
        services.AddSingleton(googleOptions);
        services.AddSingleton(googleOptionsWrapped);
        services.AddSingleton(httpClient);
        services.AddSingleton<Ghost.Platform.Google.Jobs.Internal.GoogleJobsApiClient>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Google.Jobs.Internal.GoogleJobsApiClient>>();
            return new Ghost.Platform.Google.Jobs.Internal.GoogleJobsApiClient(httpClient, googleOptions, logger);
        });
        services.AddSingleton<Ghost.Platform.Google.Jobs.Internal.GoogleJobsBrowserClient>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Google.Jobs.Internal.GoogleJobsBrowserClient>>();
            return new Ghost.Platform.Google.Jobs.Internal.GoogleJobsBrowserClient(Kernel, googleOptionsWrapped, logger);
        });
        services.AddSingleton<Ghost.Platform.Google.Jobs.Internal.GoogleJobsScraper>();
        services.AddScoped<Ghost.Platform.Google.Jobs.GoogleJobClient>();

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
