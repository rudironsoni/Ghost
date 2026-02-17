using Ghost.Plugin.Google.Jobs;
using Ghost.Plugin.Google.Jobs.Internal;
using Ghost.Testing.Fixtures;
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
        // Add logging
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        // Register HttpClient
        services.AddSingleton(_httpClient!);

        // Configure Google Jobs options
        services.Configure<GoogleJobsOptions>(options =>
        {
            options.Enabled = true;
            options.Strategy = JobSearchStrategy.HttpFirst;
        });

        // Register Google Jobs services
        services.AddSingleton<GoogleJobsApiClient>();
        services.AddSingleton<GoogleJobClient>();
    }
}
