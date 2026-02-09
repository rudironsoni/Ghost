using Ghost.Core;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.InfoJobs.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides an isolated browser context for InfoJobs integration tests.
/// Each test class gets a fresh browser session with no shared state.
/// Use with [Collection("SharedKernel")] and IClassFixture&lt;InfoJobsContextFixture&gt;.
/// </summary>
public sealed class InfoJobsContextFixture : IAsyncLifetime
{
    private readonly SharedGhostKernelFixture _kernelFixture;
    private IBrowserSession? _session;

    public InfoJobsContextFixture(SharedGhostKernelFixture kernelFixture)
    {
        _kernelFixture = kernelFixture;
    }

    public IBrowserSession Session => _session ?? throw new InvalidOperationException("Fixture not initialized");
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            // Create a fresh session for this test class
            _session = await _kernelFixture.CreateSessionAsync();

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
    }
}
