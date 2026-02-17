using Ghost.Contracts.Jobs;
using Ghost.Plugin.LinkedIn.Internal;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Testing.Fixtures;
using Ghost.Testing.Scenarios.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;

/// <summary>
/// End-to-End test fixture for LinkedIn plugin using real browser sessions.
/// Sets up a local test server and provides real IBrowserSession from RealBrowserFixture.
/// </summary>
public sealed class LinkedInE2EFixture : IAsyncLifetime
{
    private readonly RealBrowserFixture _browserFixture;
    private IServiceProvider? _serviceProvider;
    private ScenarioServer? _scenarioServer;
    private IBrowserSession? _browserSession;

    public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Fixture not initialized");
    public ScenarioServer ScenarioServer => _scenarioServer ?? throw new InvalidOperationException("Scenario server not initialized");
    public string BaseUrl => _scenarioServer?.BaseUrl ?? throw new InvalidOperationException("Scenario server not initialized");
    public IConfiguration Configuration { get; }

    public LinkedInE2EFixture(RealBrowserFixture browserFixture)
    {
        _browserFixture = browserFixture;
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start the scenario server for LinkedIn-like HTML responses
        _scenarioServer = await ScenarioServer.CreateAsync().ConfigureAwait(false);

        // Create browser session from kernel (proper async initialization)
        _browserSession = await _browserFixture.CreateSessionAsync().ConfigureAwait(false);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (_scenarioServer != null)
        {
            await _scenarioServer.StopAsync().ConfigureAwait(false);
            _scenarioServer.Dispose();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Configuration
        services.Configure<LinkedInOptions>(options =>
        {
            options.BaseUrl = BaseUrl;
            options.ScrapingStrategy = JobScrapingStrategy.Browser;
            options.ProxyEnabled = false;
            options.WarmUpEnabled = false;
        });

        services.Configure<LinkedInSessionPoolOptions>(options =>
        {
            options.MaxSize = 2;
            options.MaxIdleTime = TimeSpan.FromMinutes(30);
        });

        // Register real IBrowserSession (created in InitializeAsync)
        services.AddSingleton(_browserSession!);

        // Register LinkedIn services
        services.AddSingleton<JavaScriptAdapter>();
        services.AddSingleton<EntityParser>();
        services.AddScoped<LinkedInJobClient>();
    }
}


