using Ghost.Contracts.Jobs;
using Ghost.Kernel;
using Ghost.Plugin.Glassdoor.Internal;
using Ghost.Plugin.Glassdoor.Jobs;
using Ghost.Testing.Fixtures;
using Ghost.Testing.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ghost.Plugin.Glassdoor.End2EndTests.Fixtures;

/// <summary>
/// End-to-End test fixture for Glassdoor plugin using real browser automation.
/// Uses RealBrowserFixture for browser sessions and TestScraperServer for mock endpoints.
/// </summary>
public sealed class GlassdoorE2EFixture : IAsyncLifetime
{
    private TestScraperServer? _testServer;
    private IBrowserSession? _browserSession;

    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public TestScraperServer TestServer => _testServer ?? throw new InvalidOperationException("Test server not initialized");
    public RealBrowserFixture BrowserFixture { get; }
    public IConfiguration Configuration { get; private set; } = null!;

    public GlassdoorE2EFixture(RealBrowserFixture browserFixture)
    {
        BrowserFixture = browserFixture;
    }

    public async Task InitializeAsync()
    {
        // Start the test scraper server
        _testServer = await TestScraperServer.CreateAsync();

        // Create browser session from kernel (proper async initialization)
        _browserSession = await BrowserFixture.CreateSessionAsync();

        Configuration = new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Register the browser fixture's kernel for IGhostKernel
        services.AddSingleton(BrowserFixture.Kernel);

        // Register the browser session (created during InitializeAsync)
        services.AddSingleton(_browserSession!);

        // Configuration
        services.Configure<GlassdoorOptions>(options =>
        {
            options.Enabled = true;
            options.BaseUrl = _testServer?.GetGlassdoorBaseUrl() ?? "http://localhost:8080/glassdoor";
            options.RequestTimeoutMs = 30000;
            options.MaxRetries = 3;
            options.Strategy = JobSearchStrategy.BrowserOnly;
            options.ProxyEnabled = false;
            options.TestMode = true; // Enable test mode for faster execution
        });

        // Register HTTP client with test server base URL
        services.AddHttpClient<GlassdoorApiClient>(client =>
        {
            client.BaseAddress = new Uri(_testServer?.GetGlassdoorBaseUrl() ?? "http://localhost:8080/glassdoor");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register plugin services
        services.AddScoped<GlassdoorApiClient>(sp =>
        {
            HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GlassdoorApiClient));
            ILogger<GlassdoorApiClient> logger = sp.GetRequiredService<ILogger<GlassdoorApiClient>>();
            return new GlassdoorApiClient(httpClient, logger);
        });

        services.AddScoped<GlassdoorBrowserClient>();
        services.AddScoped<GlassdoorSearchScraper>();
        services.AddScoped<GlassdoorJobClient>();
    }

    public async Task DisposeAsync()
    {
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (_browserSession != null)
        {
            await _browserSession.DisposeAsync();
            _browserSession = null;
        }

        if (_testServer != null)
        {
            await _testServer.DisposeAsync();
            _testServer = null;
        }
    }
}
