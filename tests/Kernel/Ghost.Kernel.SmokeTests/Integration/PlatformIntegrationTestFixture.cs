using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Kernel;
using Ghost.Kernel.Services;
using Ghost.Plugin.Glassdoor;
using Ghost.Plugin.Google;
using Ghost.Plugin.Indeed;
using Ghost.Plugin.InfoJobs;
using Ghost.Plugin.LinkedIn;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Enable all platforms for testing
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true",
                ["Ghost:Extensions:Indeed:Enabled"] = "true",
                ["Ghost:Extensions:Google:Enabled"] = "true",
                ["Ghost:Extensions:Glassdoor:Enabled"] = "true",
                ["Ghost:Extensions:Glassdoor:ProxyEnabled"] = "false",
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

        // Register Ghost Kernel services
        services.AddGhost(Configuration, ghostBuilder =>
        {
            ghostBuilder.ConfigureKernel(options =>
            {
                Configuration.GetSection("Ghost:Kernel").Bind(options);
            });

            // Register enabled extensions
            if (Configuration.GetValue<bool>("Ghost:Extensions:LinkedIn:Enabled"))
            {
                ghostBuilder.UseExtension(new LinkedInPlugin());
            }

            if (Configuration.GetValue<bool>("Ghost:Extensions:Indeed:Enabled"))
            {
                ghostBuilder.UseExtension(new IndeedPlugin());
            }

            if (Configuration.GetValue<bool>("Ghost:Extensions:Google:Enabled"))
            {
                ghostBuilder.UseExtension(new GooglePlugin());
            }

            if (Configuration.GetValue<bool>("Ghost:Extensions:Glassdoor:Enabled"))
            {
                ghostBuilder.UseExtension(new GlassdoorPlugin());
            }
        });

        // Register the AggregatedJobClient as the IJobClient implementation
        services.AddScoped<IJobClient, AggregatedJobClient>();

        // Register individual platform clients as keyed services
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

    public async Task DisposeAsync()
    {
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
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
