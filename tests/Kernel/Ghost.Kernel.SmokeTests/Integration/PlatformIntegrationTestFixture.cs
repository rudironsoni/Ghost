using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Plugin.Glassdoor;
using Ghost.Plugin.Google;
using Ghost.Plugin.Indeed;
using Ghost.Plugin.InfoJobs;
using Ghost.Plugin.LinkedIn;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Smoke.Tests.Integration;

/// <summary>
/// Shared fixture for platform integration tests that configures the service provider
/// with platform clients based on configuration.
/// Only loads platforms that are explicitly enabled in configuration.
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
            .AddEnvironmentVariables();

        Configuration = builder.Build();

        // Configure services
        var services = new ServiceCollection();

        // Add core Ghost services
        services.AddLogging();
        services.AddHttpClient();

        // Register platform plugins ONLY if they are enabled
        RegisterEnabledPlugins(services, Configuration);

        // Build service provider
        ServiceProvider = services.BuildServiceProvider();

        return Task.CompletedTask;
    }

    private static void RegisterEnabledPlugins(IServiceCollection services, IConfiguration configuration)
    {
        // LinkedIn - check if enabled (default: true for integration tests)
        bool linkedInEnabled = configuration.GetValue<bool?>("Ghost:Extensions:LinkedIn:Enabled") ?? true;
        if (linkedInEnabled)
        {
            new LinkedInPlugin().ConfigureServices(services, configuration);
        }

        // Indeed - check if enabled
        bool indeedEnabled = configuration.GetValue<bool?>("Ghost:Extensions:Indeed:Enabled") ?? true;
        if (indeedEnabled)
        {
            new IndeedPlugin().ConfigureServices(services, configuration);
        }

        // Google - check if enabled
        bool googleEnabled = configuration.GetValue<bool?>("Ghost:Extensions:Google:Enabled") ?? true;
        if (googleEnabled)
        {
            new GooglePlugin().ConfigureServices(services, configuration);
        }

        // Glassdoor - check if enabled (default: false to avoid browser requirements)
        bool glassdoorEnabled = configuration.GetValue<bool?>("Ghost:Extensions:Glassdoor:Enabled") ?? false;
        if (glassdoorEnabled)
        {
            new GlassdoorPlugin().ConfigureServices(services, configuration);
        }

        // InfoJobs - check if enabled (default: false to avoid credential requirements)
        bool infoJobsEnabled = configuration.GetValue<bool?>("Ghost:Extensions:InfoJobs:Enabled") ?? false;
        if (infoJobsEnabled)
        {
            new InfoJobsPlugin().ConfigureServices(services, configuration);
        }
    }

    public Task DisposeAsync()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
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

        // Fallback to non-keyed service (for Google which doesn't use keyed registration)
        return ServiceProvider.GetRequiredService<IJobClient>();
    }

    /// <summary>
    /// Gets the IJobClient service (for platforms that don't use keyed registration).
    /// </summary>
    public IJobClient GetJobClient()
    {
        return ServiceProvider.GetRequiredService<IJobClient>();
    }
}
