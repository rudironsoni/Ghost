using Ghost.Kernel;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.LinkedIn.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides an isolated browser context for LinkedIn integration tests.
/// Each test class gets a fresh browser session with no shared state.
/// Use with [Collection("Browser")] and IClassFixture&lt;LinkedInContextFixture&gt;.
/// </summary>
public sealed class LinkedInContextFixture : IAsyncLifetime
{
    private readonly RealBrowserFixture _browserFixture;
    private IBrowserSession? _session;

    public LinkedInContextFixture(RealBrowserFixture browserFixture)
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

            // Build service provider with LinkedIn platform
            var services = new ServiceCollection();
            services.AddSingleton(_session);
            services.AddSingleton<IBrowserSession>(_session);
            services.AddLogging();

            // Register LinkedIn services manually
            services.AddOptions<Ghost.Platform.LinkedIn.LinkedInOptions>();
            services.AddSingleton<Ghost.Sdk.Spider.Adapters.JavaScriptAdapter>();
            services.AddSingleton<Ghost.Sdk.Spider.Core.Extraction.EntityParser>();
            services.AddScoped<Ghost.Platform.LinkedIn.LinkedInJobClient>();

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
