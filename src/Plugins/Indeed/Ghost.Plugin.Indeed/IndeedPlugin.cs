using System.Net.Http;
using Ghost.Contracts;
using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Http;
using Ghost.Infrastructure.Session;
using Ghost.Models;
using Ghost.Plugin.Indeed.Internal;
using Ghost.Plugin.Indeed.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.Indeed;

/// <summary>
/// Indeed plugin that provides job search and scraping capabilities.
/// </summary>
public sealed class IndeedPlugin : Ghost.Hosting.IExtension
{
    /// <inheritdoc />
    public string Name => "Indeed";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // register validator before binding options (follows InfoJobs pattern)
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<IndeedOptions>, IndeedOptionsValidator>();
        services.Configure<IndeedOptions>(configuration.GetSection("Ghost:Extensions:Indeed"));
        IndeedOptions opts = configuration.GetSection("Ghost:Extensions:Indeed").Get<IndeedOptions>() ?? new IndeedOptions();
        try { Console.WriteLine($"[DEBUG] IndeedPlugin bound options: Country={opts.Country}"); } catch { }

        if (!opts.Enabled) return;

        // register IndeedOptions for ApiClient constructor
        services.AddSingleton(opts);

        services.AddSingleton<IndeedApiClient>(sp =>
        {
            IProxyProvider? proxyProvider = sp.GetService<IProxyProvider>();
            ISessionOrchestrator? sessionOrchestrator = sp.GetService<Ghost.Infrastructure.Session.ISessionOrchestrator>();
            ILogger<IndeedApiClient> logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IndeedApiClient>>();
            IndeedOptions options = sp.GetRequiredService<IndeedOptions>();

            if (proxyProvider != null && sessionOrchestrator != null)
            {
                return new IndeedApiClient(proxyProvider, sessionOrchestrator, options, logger);
            }

            if (sessionOrchestrator != null)
            {
                return new IndeedApiClient(sessionOrchestrator, options, logger);
            }

            if (proxyProvider != null)
            {
                return new IndeedApiClient(proxyProvider, options, logger);
            }

            throw new InvalidOperationException("IndeedApiClient requires IProxyProvider or ISessionOrchestrator to be registered.");
        });

        // Register scrapers (require browser session)
        services.AddScoped<Jobs.IndeedSearchScraper>(sp =>
        {
            IndeedApiClient apiClient = sp.GetRequiredService<IndeedApiClient>();
            ILogger<IndeedSearchScraper> logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Jobs.IndeedSearchScraper>>();
            IBrowserSession? browserSession = sp.GetService<IBrowserSession>();
            IndeedOptions options = sp.GetRequiredService<IndeedOptions>();
            return new Jobs.IndeedSearchScraper(apiClient, logger, browserSession, options);
        });

        services.AddScoped<Jobs.IndeedJobDetailsScraper>(sp =>
        {
            IBrowserSession browserSession = sp.GetService<IBrowserSession>()
                ?? throw new InvalidOperationException("IndeedJobDetailsScraper requires IBrowserSession to be registered.");

            ILogger<IndeedJobDetailsScraper> logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Jobs.IndeedJobDetailsScraper>>();
            IJsonLdExtractor? jsonLdExtractor = sp.GetService<Ghost.IJsonLdExtractor>();
            IndeedOptions options = sp.GetRequiredService<IndeedOptions>();
            return new Jobs.IndeedJobDetailsScraper(browserSession, logger, jsonLdExtractor, options);
        });

        services.AddScoped<IndeedJobClient>(sp =>
        {
            IndeedApiClient apiClient = sp.GetRequiredService<IndeedApiClient>();
            ILogger<IndeedJobClient> logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IndeedJobClient>>();
            IndeedSearchScraper? searchScraper = sp.GetService<Jobs.IndeedSearchScraper>();
            IndeedJobDetailsScraper? detailsScraper = sp.GetService<Jobs.IndeedJobDetailsScraper>();
            return new IndeedJobClient(apiClient, logger, searchScraper, detailsScraper);
        });

        // register as both IJobScraper and IJobClient for backward compatibility
        services.AddScoped<Ghost.IJobScraper>(sp => sp.GetRequiredService<IndeedJobClient>());
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<IndeedJobClient>());

        // Register plugin-specific services
        services.AddSingleton<IndeedPluginCapabilities>(sp => new IndeedPluginCapabilities
        {
            RequiresBrowser = true,
            RequiresProxy = false,
            SupportsJobs = true,
            SupportsSocial = false,
            SupportsNews = false
        });

        services.AddSingleton<IIndeedPluginReadinessCheck, IndeedPluginReadinessCheck>();

        // Register keyed IJobClient mapping for worker compatibility.
        services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("indeed", (sp, _) =>
            sp.GetRequiredService<IndeedJobClient>());
    }
}
