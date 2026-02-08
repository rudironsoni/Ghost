using Ghost.Core;
using Ghost.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.InfoJobs.Integration.Fixtures;

/// <summary>
/// Test fixture that provides a GhostKernel instance for InfoJobs integration tests.
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

        // Build service provider with InfoJobs platform
        var services = new ServiceCollection();
        services.AddSingleton(Session);
        services.AddSingleton<IBrowserSession>(Session);
        services.AddLogging();

        // Register InfoJobs services manually
        var infoJobsOptions = new Ghost.Platform.InfoJobs.Jobs.InfoJobsOptions();
        var httpClient = new System.Net.Http.HttpClient();

        services.AddSingleton(infoJobsOptions);
        services.AddSingleton(httpClient);
        services.AddSingleton<Ghost.Platform.InfoJobs.Jobs.Internal.InfoJobsApiClient>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.InfoJobs.Jobs.Internal.InfoJobsApiClient>>();
            return new Ghost.Platform.InfoJobs.Jobs.Internal.InfoJobsApiClient(httpClient, infoJobsOptions, logger);
        });
        services.AddScoped<Ghost.Platform.InfoJobs.Jobs.InfoJobClient>();

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
