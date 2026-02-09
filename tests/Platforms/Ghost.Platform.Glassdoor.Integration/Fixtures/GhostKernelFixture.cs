using Ghost.Core;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Glassdoor.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides an isolated browser context for Glassdoor integration tests.
/// Each test class gets a fresh browser session with no shared state.
/// Use with [Collection("Browser")] and IClassFixture&lt;GlassdoorContextFixture&gt;.
/// </summary>
public sealed class GlassdoorContextFixture : IAsyncLifetime
{
    private readonly RealBrowserFixture _browserFixture;
    private IBrowserSession? _session;

    public GlassdoorContextFixture(RealBrowserFixture browserFixture)
    {
        _browserFixture = browserFixture;
    }

    public IBrowserSession Session => _session ?? throw new InvalidOperationException("Fixture not initialized");
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            // Create a fresh session for this test class
            _session = await _browserFixture.CreateSessionAsync();

            // Build service provider with Glassdoor platform
            var services = new ServiceCollection();
            services.AddSingleton(_session);
            services.AddSingleton<IBrowserSession>(_session);
            services.AddLogging();

            // Register Glassdoor services manually
            var glassdoorOptions = Microsoft.Extensions.Options.Options.Create(new Ghost.Platform.Glassdoor.GlassdoorOptions());
            var proxyProvider = Ghost.Proxy.StaticProxyProvider.Empty;

            services.AddSingleton(_browserFixture.ConcreteKernel);
            services.AddSingleton(glassdoorOptions);
            services.AddSingleton<Ghost.Abstractions.IProxyProvider>(proxyProvider);
            services.AddSingleton<System.Net.Http.HttpClient>();
            services.AddSingleton<Ghost.Platform.Glassdoor.Internal.GlassdoorApiClient>();
            services.AddSingleton<Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient>>();
                return new Ghost.Platform.Glassdoor.Internal.GlassdoorBrowserClient(_browserFixture.ConcreteKernel, glassdoorOptions, logger, proxyProvider);
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
