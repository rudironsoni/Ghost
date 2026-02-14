using System.Net.Http;
using Ghost.Abstractions;
using Ghost.Contracts;
using Ghost.Core;
using Ghost.Hosting;
using Ghost.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Glassdoor;

public sealed class GlassdoorExtension : Ghost.Hosting.IExtension
{
    public string Name => "Glassdoor";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices { get { Console.WriteLine("[DEBUG] GlassdoorExtension.RequiredServices called"); return Array.Empty<Type>(); } }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GlassdoorOptions>(configuration.GetSection("Ghost:Extensions:Glassdoor"));

        // Register GlassdoorApiClient with a factory that creates an HttpClient with proper configuration
        services.AddScoped<Internal.GlassdoorApiClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GlassdoorOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<Internal.GlassdoorApiClient>>();
            var proxyProvider = sp.GetService<IProxyProvider>();

            var handler = opts.ProxyEnabled && proxyProvider != null
                ? new HttpClientHandler { Proxy = new RotatingWebProxy(proxyProvider), UseProxy = true }
                : new HttpClientHandler { UseProxy = false };

            var configuredHandler = HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);
            var httpClient = new HttpClient(configuredHandler)
            {
                Timeout = TimeSpan.FromMilliseconds(opts.RequestTimeoutMs)
            };

            return new Internal.GlassdoorApiClient(httpClient, logger);
        });

        // Register browser fallback client WITHOUT proxy support to avoid SOCKS5 auth issues
        services.AddScoped<Internal.GlassdoorBrowserClient>(sp =>
        {
            var kernel = sp.GetRequiredService<IGhostKernel>();
            var options = sp.GetRequiredService<IOptions<GlassdoorOptions>>();
            var logger = sp.GetRequiredService<ILogger<Internal.GlassdoorBrowserClient>>();
            // Explicitly pass null for proxy provider to disable proxy for Glassdoor browser client
            return new Internal.GlassdoorBrowserClient(kernel, options, logger, proxyProvider: null);
        });

        // Register heavy stealth browser scraper with optional proxy support
        services.AddScoped<Jobs.GlassdoorSearchScraper>(sp =>
        {
            var kernel = sp.GetRequiredService<IGhostKernel>();
            var options = sp.GetRequiredService<IOptions<GlassdoorOptions>>();
            var logger = sp.GetRequiredService<ILogger<Jobs.GlassdoorSearchScraper>>();
            var proxyProvider = sp.GetService<IProxyProvider>();
            // Proxy provider is optional - scraper will use it only if ProxyEnabled is true
            return new Jobs.GlassdoorSearchScraper(kernel, options, logger, proxyProvider);
        });

        // Register GlassdoorJobClient and expose it as both IJobScraper and IJobClient
        // IJobScraper is used by AggregatedJobClient, IJobClient for backward compatibility
        services.AddScoped<GlassdoorJobClient>();
        services.AddScoped<Ghost.Abstractions.IJobScraper>(sp => sp.GetRequiredService<GlassdoorJobClient>());
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<GlassdoorJobClient>());
    }
}
