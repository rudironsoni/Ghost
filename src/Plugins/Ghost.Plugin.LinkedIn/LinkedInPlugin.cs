using Ghost.Abstractions;
using Ghost.Core;
using Ghost.Hosting;
using Ghost.Plugin.LinkedIn.Internal;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.LinkedIn;

/// <summary>
/// LinkedIn plugin that provides job, social, and news client implementations.
/// </summary>
public sealed class LinkedInPlugin : IExtension
{
    public string Name => "LinkedIn";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Social.ISocialClient), typeof(Ghost.Contracts.Jobs.IJobClient), typeof(Ghost.Contracts.News.INewsClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new LinkedInPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:LinkedIn:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:LinkedIn:RegisterReadinessServices", true),
            RegisterKeyedJobClient = configuration.GetValue("Ghost:Plugins:LinkedIn:RegisterKeyedJobClient", true)
        };

        // Bind from nested path: Ghost:Extensions:LinkedIn in appsettings.json
        services.Configure<LinkedInOptions>(configuration.GetSection("Ghost:Extensions:LinkedIn"));
        services.Configure<LinkedInSessionPoolOptions>(configuration.GetSection("Ghost:Extensions:LinkedIn:SessionPool"));

        // Use factory method to prevent resolution during DI container validation
        services.AddSingleton<Internal.LinkedInSessionPool>(sp =>
        {
            IGhostKernel kernel = sp.GetRequiredService<Ghost.Core.IGhostKernel>();
            LinkedInSessionPoolOptions poolOptions = sp.GetRequiredService<IOptions<LinkedInSessionPoolOptions>>().Value;
            ILogger<LinkedInSessionPool> logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Internal.LinkedInSessionPool>>();
            IProxyProvider? proxyProvider = sp.GetService<Ghost.Abstractions.IProxyProvider>();
            LinkedInOptions linkedInOptions = sp.GetRequiredService<IOptions<LinkedInOptions>>().Value;
            return Internal.LinkedInSessionPool.Create(kernel, poolOptions, logger, proxyProvider, linkedInOptions);
        });

        // Register platform-specific implementations for core abstractions
        services.AddSingleton<Ghost.Abstractions.ITextExtractor, Internal.LinkedInTextExtractor>();
        services.AddSingleton<Ghost.Abstractions.ICountryDomainProvider, Internal.LinkedInCountryProvider>();
        // Ensure JsonLdExtractor from Core utilities is available to this platform
        services.AddSingleton<Ghost.Abstractions.IJsonLdExtractor, Ghost.Utilities.JsonLdExtractor>();
        // GuestJobSearch implements guest API scraping logic - Singleton since it only uses the session pool
        services.AddSingleton<Internal.IGuestJobSearch, Internal.GuestJobSearch>();

        // Register Spider SDK services required by LinkedInJobClient
        services.AddSingleton<JavaScriptAdapter>();
        services.AddSingleton<EntityParser>();

        // Register concrete implementations as Scoped (not Singleton) because they depend on IBrowserSession
        services.AddScoped<LinkedInSocialClient>();
        services.AddScoped<LinkedInJobClient>();
        services.AddScoped<LinkedInNewsClient>();

        // Authenticator used by LinkedInSocialClient for logging in / cookie handling - Scoped because it uses IBrowserSession
        services.AddScoped<Internal.LinkedInAuthenticator>();

        // Register interface mappings (for when aggregators need them)
        services.AddScoped<Ghost.Contracts.Social.ISocialClient>(sp => sp.GetRequiredService<LinkedInSocialClient>());
        services.AddScoped<Ghost.Abstractions.IJobScraper>(sp => sp.GetRequiredService<LinkedInJobClient>());
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<LinkedInJobClient>());
        services.AddScoped<Ghost.Contracts.News.INewsClient>(sp => sp.GetRequiredService<LinkedInNewsClient>());

        if (pluginOptions.RegisterReadinessServices)
        {
            // Register plugin-specific services
            services.AddSingleton<LinkedInPluginCapabilities>(sp => new LinkedInPluginCapabilities
            {
                RequiresBrowser = true,
                RequiresProxy = false,
                SupportsJobs = true,
                SupportsSocial = true,
                SupportsNews = true
            });

            services.AddSingleton<ILinkedInPluginReadinessCheck, LinkedInPluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedJobClient)
        {
            // Register keyed IJobClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("linkedin", (sp, _) =>
                sp.GetRequiredService<LinkedInJobClient>());
        }
    }
}
