using Ghost.Kernel;
using Ghost.Kernel.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.InfoJobs.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides an isolated browser context for InfoJobs integration tests.
/// Each test class gets a fresh browser session with no shared state.
/// Use with IClassFixture&lt;InfoJobsContextFixture&gt;.
/// </summary>
public sealed class InfoJobsContextFixture : IAsyncLifetime
{
    private GhostKernel? _kernel;
    private IBrowserSession? _session;

    public InfoJobsContextFixture()
    {
    }

    public IBrowserSession Session => _session ?? throw new InvalidOperationException("Fixture not initialized");
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            // Create a GhostKernel instance for this test class
            var options = new KernelOptions
            {
                EnableStealth = true,
                Headless = true
            };
            _kernel = await GhostKernel.CreateAsync(options);

            // Create a fresh session for this test class
            _session = await _kernel.NewSessionAsync();

            // Build service provider with InfoJobs platform
            var services = new ServiceCollection();
            services.AddSingleton(_session);
            services.AddSingleton<IBrowserSession>(_session);
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
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_session != null)
        {
            await _session.DisposeAsync();
        }

        if (_kernel != null)
        {
            await _kernel.DisposeAsync();
        }
    }
}
