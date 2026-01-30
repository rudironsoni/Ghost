using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Contracts;
using Ghost.Hosting;
using Ghost.Abstractions;
using Ghost.Http;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Ghost.Core;

namespace Ghost.Platform.Glassdoor;

public sealed class GlassdoorExtension : Ghost.Hosting.IExtension
{
    public string Name => "Glassdoor";
    public Version Version => new(1,0,0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(GhostKernel) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GlassdoorOptions>(configuration.GetSection("Ghost:Extensions:Glassdoor"));

        // AddHttpClient extension lives in Microsoft.Extensions.Http package - ensure callers include it.
        services.AddHttpClient<Internal.GlassdoorApiClient>()
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<GlassdoorOptions>>().Value;
                var handler = opts.ProxyEnabled
                    ? new HttpClientHandler { Proxy = new RotatingWebProxy(sp.GetRequiredService<IProxyProvider>()), UseProxy = true }
                    : new HttpClientHandler { UseProxy = false };

                return HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);
            });

        // Register browser fallback client
        services.AddScoped<Internal.GlassdoorBrowserClient>(sp =>
        {
            var kernel = sp.GetRequiredService<GhostKernel>();
            var options = sp.GetRequiredService<IOptions<GlassdoorOptions>>();
            var logger = sp.GetRequiredService<ILogger<Internal.GlassdoorBrowserClient>>();
            var proxyProvider = sp.GetService<IProxyProvider>();
            return new Internal.GlassdoorBrowserClient(kernel, options, logger, proxyProvider);
        });

        // register as IJobScraper and IJobClient
        services.AddScoped<Ghost.Abstractions.IJobScraper, GlassdoorJobClient>();
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<Ghost.Abstractions.IJobScraper>() as Ghost.Contracts.Jobs.IJobClient ?? sp.GetRequiredService<GlassdoorJobClient>());
    }
}
