using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Ghost.Smoke.Tests.Smoke;

namespace Ghost.Smoke.Tests.Integration;

/// <summary>
/// Shared fixture for platform integration tests that configures the service provider
/// with stub job clients to avoid external service calls.
/// </summary>
public class PlatformIntegrationTestFixture : IAsyncLifetime
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;

    public Task InitializeAsync()
    {
        // Build configuration
        IConfigurationBuilder builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Use stubs - disable real plugins
                ["Ghost:Extensions:LinkedIn:Enabled"] = "false",
                ["Ghost:Extensions:Indeed:Enabled"] = "false",
                ["Ghost:Extensions:Google:Enabled"] = "false",
                ["Ghost:Extensions:Glassdoor:Enabled"] = "false",
                ["Ghost:Extensions:InfoJobs:Enabled"] = "false",
                ["Ghost:Kernel:Headless"] = "true",
                ["Ghost:Kernel:MaxConcurrentSessions"] = "2"
            })
            .AddEnvironmentVariables();

        Configuration = builder.Build();

        // Configure services
        var services = new ServiceCollection();

        // Add core Ghost services
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddHttpClient();

        // Register stub job clients directly
        services.AddSingleton<IJobClient>(sp => new StubJobClient("LinkedIn"));
        services.AddSingleton<IJobClient>(sp => new StubJobClient("Indeed"));
        services.AddSingleton<IJobClient>(sp => new StubJobClient("Google"));
        services.AddSingleton<IJobClient>(sp => new StubJobClient("Glassdoor"));

        // Register as keyed services
        services.AddKeyedSingleton<IJobClient, StubJobClient>("linkedin", (sp, key) => new StubJobClient("LinkedIn"));
        services.AddKeyedSingleton<IJobClient, StubJobClient>("indeed", (sp, key) => new StubJobClient("Indeed"));
        services.AddKeyedSingleton<IJobClient, StubJobClient>("google", (sp, key) => new StubJobClient("Google"));
        services.AddKeyedSingleton<IJobClient, StubJobClient>("glassdoor", (sp, key) => new StubJobClient("Glassdoor"));

        // Build service provider
        ServiceProvider = services.BuildServiceProvider();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a keyed IJobClient for the specified platform.
    /// </summary>
    public IJobClient GetJobClient(string platformKey)
    {
        IJobClient? keyService = ServiceProvider.GetKeyedService<IJobClient>(platformKey);
        if (keyService != null)
        {
            return keyService;
        }

        return ServiceProvider.GetRequiredService<IJobClient>();
    }

    /// <summary>
    /// Gets the IJobClient service.
    /// </summary>
    public IJobClient GetJobClient()
    {
        return ServiceProvider.GetRequiredService<IJobClient>();
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
    }
}
