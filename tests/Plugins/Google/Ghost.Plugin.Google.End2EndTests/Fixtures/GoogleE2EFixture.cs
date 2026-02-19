using Ghost.Plugin.Google.Gemini;
using Ghost.Plugin.Google.Jobs;
using Ghost.Plugin.Google.Jobs.Internal;
using Ghost.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        _httpClient = new HttpClient();
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
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add logging first
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        // Register HttpClient for direct HTTP-based API client
        services.AddSingleton(_httpClient!);

        // Configure Google Jobs options
        services.Configure<GoogleJobsOptions>(options =>
        {
            options.Enabled = true;
            options.Strategy = JobSearchStrategy.HttpFirst;
        });

        // Configure Gemini options
        services.Configure<Gemini.GeminiOptions>(options =>
        {
            options.BaseUrl = "https://gemini.google.com";
            options.DefaultModel = "gemini-1.5-flash";
            options.ResponseTimeout = TimeSpan.FromSeconds(60);
        });

        // Register production GoogleJobsApiClient so E2E uses the same codepath as runtime.
        services.AddSingleton<Jobs.Internal.GoogleJobsApiClient>(sp =>
        {
            ILogger<Jobs.Internal.GoogleJobsApiClient> logger = sp.GetRequiredService<ILogger<Jobs.Internal.GoogleJobsApiClient>>();
            GoogleJobsOptions options = sp.GetRequiredService<IOptions<GoogleJobsOptions>>().Value;
            return new Jobs.Internal.GoogleJobsApiClient(sp.GetRequiredService<HttpClient>(), options, logger);
        });

        // Register GoogleJobClient
        services.AddSingleton<GoogleJobClient>();

        // Register IBrowserSession with a fake for E2E testing
        services.AddSingleton<IBrowserSession, FakeBrowserSession>();

        // Register GeminiClient
        services.AddSingleton<Gemini.GeminiClient>();
    }
}
