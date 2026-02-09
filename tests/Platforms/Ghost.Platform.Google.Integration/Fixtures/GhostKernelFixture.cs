using Ghost.Core;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Google.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides an isolated browser context for Google Jobs integration tests.
/// Each test class gets a fresh browser session with no shared state.
/// Use with [Collection("SharedKernel")] and IClassFixture&lt;GoogleContextFixture&gt;.
/// </summary>
public sealed class GoogleContextFixture : IAsyncLifetime
{
    private readonly SharedGhostKernelFixture _kernelFixture;
    private IBrowserSession? _session;

    public GoogleContextFixture(SharedGhostKernelFixture kernelFixture)
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

            // Build service provider with Google Jobs platform
            var services = new ServiceCollection();
            services.AddSingleton(_session);
            services.AddSingleton<IBrowserSession>(_session);
            services.AddLogging();

            // Register Google Jobs services manually
            var googleOptions = new Ghost.Platform.Google.Jobs.GoogleJobsOptions();
            var googleOptionsWrapped = Microsoft.Extensions.Options.Options.Create(googleOptions);
            var httpClient = new System.Net.Http.HttpClient();

            services.AddSingleton(_kernelFixture.ConcreteKernel);
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
                return new Ghost.Platform.Google.Jobs.Internal.GoogleJobsBrowserClient(_kernelFixture.ConcreteKernel, googleOptionsWrapped, logger);
            });
            services.AddSingleton<Ghost.Platform.Google.Jobs.Internal.GoogleJobsScraper>();
            services.AddScoped<Ghost.Platform.Google.Jobs.GoogleJobClient>();

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
