using System.Reflection;
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

namespace Ghost.Smoke.Tests.Platforms;

/// <summary>
/// Shared fixture for platform smoke tests that configures the service provider
/// with all platform clients and required dependencies.
/// </summary>
public class PlatformSmokeTestFixture : IAsyncLifetime
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;

    public Task InitializeAsync()
    {
        // Build configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        Configuration = builder.Build();

        // Configure services
        var services = new ServiceCollection();

        // Add core Ghost services
        services.AddLogging();
        services.AddHttpClient();

        // Register platform plugins
        var linkedInPlugin = new LinkedInPlugin();
        var indeedPlugin = new IndeedPlugin();
        var glassdoorPlugin = new GlassdoorPlugin();
        var googlePlugin = new GooglePlugin();
        var infoJobsPlugin = new InfoJobsPlugin();

        linkedInPlugin.ConfigureServices(services, Configuration);
        indeedPlugin.ConfigureServices(services, Configuration);
        glassdoorPlugin.ConfigureServices(services, Configuration);
        googlePlugin.ConfigureServices(services, Configuration);
        infoJobsPlugin.ConfigureServices(services, Configuration);

        // Build service provider
        ServiceProvider = services.BuildServiceProvider();

        return Task.CompletedTask;
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
        var keyService = ServiceProvider.GetKeyedService<IJobClient>(platformKey);
        if (keyService != null)
        {
            return keyService;
        }

        // Fallback to non-keyed service (for Google which doesn't use keyed registration)
        return ServiceProvider.GetRequiredService<IJobClient>();
    }
}
