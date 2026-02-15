using Ghost.Core;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Glassdoor.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides an isolated browser context for Glassdoor integration tests.
/// Each test class gets a fresh browser session with no shared state.
/// Use with [Collection("SharedKernel")] and IClassFixture&lt;GlassdoorContextFixture&gt;.
/// </summary>
public sealed class GlassdoorContextFixture : IAsyncLifetime
{
    private readonly SharedGhostKernelFixture _kernelFixture;
    private IBrowserSession? _session;

    public GlassdoorContextFixture(SharedGhostKernelFixture kernelFixture)
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

            // Build service provider with Glassdoor platform
            var services = new ServiceCollection();
            services.AddSingleton(_session);
            services.AddSingleton<IBrowserSession>(_session);
            services.AddLogging();

            // Register Glassdoor services manually
            var glassdoorOptions = Microsoft.Extensions.Options.Options.Create(new Ghost.Platform.Glassdoor.GlassdoorOptions
            {
                Strategy = Ghost.Platform.Glassdoor.JobSearchStrategy.HttpOnly,
                Enabled = false,
                MaxRetries = 0,
                EnableRetryWithJitter = false,
                RequestTimeoutMs = 8000
            });
            var proxyProvider = Ghost.Proxy.StaticProxyProvider.Empty;

            services.AddSingleton(_kernelFixture.ConcreteKernel);
            services.AddSingleton(glassdoorOptions);
            services.AddSingleton<Ghost.Abstractions.IProxyProvider>(proxyProvider);
            services.AddSingleton<System.Net.Http.HttpClient>();
            services.AddSingleton<Ghost.Platform.Glassdoor.Internal.GlassdoorApiClient>(sp =>
            {
                var httpClient = sp.GetRequiredService<System.Net.Http.HttpClient>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Glassdoor.Internal.GlassdoorApiClient>>();
                return new Ghost.Platform.Glassdoor.Internal.GlassdoorApiClient(httpClient, logger);
            });
            services.AddSingleton<Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient>>();
                return new Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient(_kernelFixture.ConcreteKernel, glassdoorOptions, logger, proxyProvider);
            });
            services.AddSingleton<Ghost.Platform.Glassdoor.Jobs.GlassdoorSearchScraper>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Glassdoor.Jobs.GlassdoorSearchScraper>>();
                return new Ghost.Platform.Glassdoor.Jobs.GlassdoorSearchScraper(_kernelFixture.ConcreteKernel, glassdoorOptions, logger, proxyProvider);
            });
            services.AddScoped<Ghost.Platform.Glassdoor.GlassdoorJobClient>();

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
