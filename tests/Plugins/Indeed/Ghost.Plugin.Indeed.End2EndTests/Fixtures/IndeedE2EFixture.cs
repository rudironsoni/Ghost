using Ghost.Kernel;
using Ghost.Models;
using Ghost.Plugin.Indeed.Internal;
using Ghost.Testing.Fixtures;
using Ghost.Testing.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ghost.Plugin.Indeed.End2EndTests.Fixtures;

/// <summary>
/// End-to-End test fixture for Indeed plugin using real browser automation.
/// Sets up dependency injection container with real IBrowserSession from GhostKernel.
/// Tests run against localhost TestScraperServer for realistic HTML fixtures.
/// </summary>
public sealed class IndeedE2EFixture : IAsyncLifetime
{
    private readonly RealBrowserFixture _browserFixture;
    private IBrowserSession? _session;
    private TestScraperServer? _testServer;

    public IndeedE2EFixture(RealBrowserFixture browserFixture)
    {
        _browserFixture = browserFixture;
    }

    public IBrowserSession Session => _session ?? throw new InvalidOperationException("Fixture not initialized");
    public TestScraperServer TestServer => _testServer ?? throw new InvalidOperationException("Test server not initialized");
    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;
    public string IndeedBaseUrl => _testServer?.GetIndeedBaseUrl() ?? throw new InvalidOperationException("Test server not initialized");

    public async Task InitializeAsync()
    {
        try
        {
            // Start the TestScraperServer for Indeed HTML fixtures
            _testServer = await TestScraperServer.CreateAsync().ConfigureAwait(false);

            // Create a fresh session for this test class
            _session = await _browserFixture.CreateSessionAsync().ConfigureAwait(false);

            // Build configuration
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("testsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Build service provider with Indeed services
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Configuration
        var options = new IndeedOptions
        {
            Enabled = true,
            Country = CountryCode.US,
            BaseUrl = IndeedBaseUrl,
            ApiEndpoint = $"{IndeedBaseUrl}/graphql",
            ApiKey = "test-api-key",
            RequestTimeoutMs = 30000,
            MaxRetries = 3,
            ResultsPerPage = 25
        };
        services.AddSingleton(options);

        // Register real browser session
        services.AddSingleton(_session!);
        services.AddSingleton<IBrowserSession>(_session!);

        // Register Indeed services
        services.AddSingleton<IndeedApiClient>(sp =>
        {
            ILogger<IndeedApiClient> logger = sp.GetRequiredService<ILogger<IndeedApiClient>>();
            return new IndeedApiClient(sp.GetService<IProxyProvider>()!, sp.GetRequiredService<IndeedOptions>(), logger);
        });

        services.AddScoped<IndeedJobClient>();
    }

    public async Task DisposeAsync()
    {
        if (_session != null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
        }

        if (_testServer != null)
        {
            await _testServer.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Collection attribute for Indeed E2E tests using real browser.
/// Tests in the same collection run sequentially and share the RealBrowserFixture.
/// </summary>
[CollectionDefinition("Browser")]
public class IndeedE2EFixtures : ICollectionFixture<RealBrowserFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
