using System.Net.Http;
using Ghost.Contracts;
using Ghost.Hosting;
using Ghost.Http;
using Ghost.Kernel;
using Ghost.Plugin.Glassdoor.Internal;
using Ghost.Plugin.Glassdoor.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Glassdoor;

/// <summary>
/// Glassdoor plugin that provides job search functionality.
/// </summary>
public sealed class GlassdoorPlugin : Ghost.Hosting.IExtension
{
    public string Name => "Glassdoor";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GlassdoorOptions>(configuration.GetSection("Ghost:Extensions:Glassdoor"));

        // Register GlassdoorApiClient with a factory that creates an HttpClient with proper configuration
        services.AddScoped<Internal.GlassdoorApiClient>(sp =>
        {
            GlassdoorOptions opts = sp.GetRequiredService<IOptions<GlassdoorOptions>>().Value;
            ILogger<GlassdoorApiClient> logger = sp.GetRequiredService<ILogger<Internal.GlassdoorApiClient>>();
            IProxyProvider? proxyProvider = sp.GetService<IProxyProvider>();

            HttpClientHandler handler = opts.ProxyEnabled && proxyProvider != null
                ? new HttpClientHandler { Proxy = new RotatingWebProxy(proxyProvider), UseProxy = true }
                : new HttpClientHandler { UseProxy = false };

            HttpClientHandler configuredHandler = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);
            var httpClient = new HttpClient(configuredHandler)
            {
                Timeout = TimeSpan.FromMilliseconds(opts.RequestTimeoutMs)
            };

            return new Internal.GlassdoorApiClient(httpClient, logger);
        });

        // Register browser fallback client WITHOUT proxy support to avoid SOCKS5 auth issues
        services.AddScoped<Internal.GlassdoorBrowserClient>(sp =>
        {
            IGhostKernel kernel = sp.GetRequiredService<IGhostKernel>();
            IOptions<GlassdoorOptions> options = sp.GetRequiredService<IOptions<GlassdoorOptions>>();
            ILogger<GlassdoorBrowserClient> logger = sp.GetRequiredService<ILogger<Internal.GlassdoorBrowserClient>>();
            // Explicitly pass null for proxy provider to disable proxy for Glassdoor browser client
            return new Internal.GlassdoorBrowserClient(kernel, options, logger, proxyProvider: null);
        });

        // Register heavy stealth browser scraper with optional proxy support
        services.AddScoped<Jobs.GlassdoorSearchScraper>(sp =>
        {
            IGhostKernel kernel = sp.GetRequiredService<IGhostKernel>();
            IOptions<GlassdoorOptions> options = sp.GetRequiredService<IOptions<GlassdoorOptions>>();
            ILogger<GlassdoorSearchScraper> logger = sp.GetRequiredService<ILogger<Jobs.GlassdoorSearchScraper>>();
            IProxyProvider? proxyProvider = sp.GetService<IProxyProvider>();
            // Proxy provider is optional - scraper will use it only if ProxyEnabled is true
            return new Jobs.GlassdoorSearchScraper(kernel, options, logger, proxyProvider);
        });

        // Register GlassdoorJobClient and expose it as both IJobScraper and IJobClient
        // IJobScraper is used by AggregatedJobClient, IJobClient for backward compatibility
        services.AddScoped<GlassdoorJobClient>();
        services.AddScoped<Ghost.IJobScraper>(sp => sp.GetRequiredService<GlassdoorJobClient>());
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<GlassdoorJobClient>());

        // Register plugin-specific services
        services.AddSingleton<GlassdoorPluginCapabilities>(sp => new GlassdoorPluginCapabilities
        {
            RequiresBrowser = true,
            RequiresProxy = false,
            SupportsJobs = true,
            SupportsSocial = false,
            SupportsNews = false
        });

        services.AddSingleton<IGlassdoorPluginReadinessCheck, GlassdoorPluginReadinessCheck>();

        // Register keyed IJobClient mapping for worker compatibility.
        services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("glassdoor", (sp, _) =>
            sp.GetRequiredService<GlassdoorJobClient>());
    }
}
