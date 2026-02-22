using Ghost.Platform.Storage.Session;
using Ghost.Plugin.Google.Gemini;
using Ghost.Plugin.Google.Jobs;
using Ghost.Plugin.Google.Jobs.Internal;
using Ghost.Pool;
using Ghost.Testing.External.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ghost.Plugin.Google.End2EndTests.Fixtures;

/// <summary>
/// Fixture for Google End-to-End tests using real browser infrastructure.
/// </summary>
#pragma warning disable CA1001 // IAsyncLifetime handles disposal
public sealed class GoogleE2EFixture : IAsyncLifetime
#pragma warning restore CA1001
{
    private IServiceProvider? _serviceProvider;
    private HttpClient? _httpClient;

    public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Fixture not initialized");

    public GoogleE2EFixture()
    {
    }

    public async Task InitializeAsync()
    {
        var cassetteStore = new CassetteStore(ResolveCassetteDirectory());
        CassetteMode mode = CassetteModeResolver.FromEnvironment();
        var cassetteHandler = new CassetteDelegatingHandler(cassetteStore, mode)
        {
            InnerHandler = new HttpClientHandler()
        };
        _httpClient = new HttpClient(cassetteHandler, disposeHandler: true);
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add logging first
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        // Configure Google Jobs options - use BrowserFirst strategy (production default)
        var googleJobsOptions = new GoogleJobsOptions
        {
            Enabled = true,
            Strategy = JobSearchStrategy.BrowserFirst
        };
        services.Configure<GoogleJobsOptions>(options =>
        {
            options.Enabled = googleJobsOptions.Enabled;
            options.Strategy = googleJobsOptions.Strategy;
        });
        // Also register as singleton for direct injection (required by GoogleJobsApiClient constructor)
        services.AddSingleton(googleJobsOptions);

        // Configure Gemini options
        services.Configure<Gemini.GeminiOptions>(options =>
        {
            options.BaseUrl = "https://gemini.google.com";
            options.DefaultModel = "gemini-1.5-flash";
            options.ResponseTimeout = TimeSpan.FromSeconds(60);
        });

        // Register SessionOrchestrator dependencies required for production codepath
        services.AddSingleton<Ghost.IProxyProvider>(Ghost.Proxy.StaticProxyProvider.Empty);

        // Register a minimal browser pool mock - Google E2E tests rely on HTTP via cassettes
        services.AddSingleton<ITieredBrowserPool>(Substitute.For<ITieredBrowserPool>());

        // Register SessionOrchestrator - this enables the production codepath for GoogleJobsApiClient
        services.AddSessionOrchestrator(options =>
        {
            options.MaxConcurrentHttpSessions = 10;
            options.MaxConcurrentBrowserSessions = 5;
            options.DefaultSessionTtl = TimeSpan.FromMinutes(30);
            options.EnableAutoRecycling = false;
        });

        // Register GoogleJobsApiClient via DI - uses [ActivatorUtilitiesConstructor] with ISessionOrchestrator
        // This ensures E2E tests use the same production codepath as runtime
        services.AddSingleton<Jobs.Internal.GoogleJobsApiClient>();

        // Register GoogleJobClient
        services.AddSingleton<GoogleJobClient>();

        // Register GeminiClient
        services.AddSingleton<Gemini.GeminiClient>();
    }

    private static string ResolveCassetteDirectory()
    {
        string? configuredDirectory = Environment.GetEnvironmentVariable("GHOST_CASSETTE_DIR");
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return configuredDirectory;
        }

        return Path.Combine(AppContext.BaseDirectory, "Cassettes", "Recordings");
    }
}
