using Microsoft.Extensions.Configuration;
using Ghost.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.LinkedIn;

/// <summary>
/// Registers LinkedIn-related clients.
/// </summary>
public sealed class LinkedInExtension : IExtension
{
    public string Name => "LinkedIn";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Social.ISocialClient), typeof(Ghost.Contracts.Jobs.IJobClient), typeof(Ghost.Contracts.News.INewsClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Bind from nested path: Ghost:Extensions:LinkedIn in appsettings.json
        services.Configure<LinkedInOptions>(configuration.GetSection("Ghost:Extensions:LinkedIn"));
        services.Configure<LinkedInSessionPoolOptions>(configuration.GetSection("Ghost:Extensions:LinkedIn:SessionPool"));
        // Authenticator used by LinkedInSocialClient for logging in / cookie handling
        services.AddTransient<Internal.LinkedInAuthenticator>();
        services.AddSingleton<Internal.LinkedInSessionPool>(sp =>
        {
            var kernel = sp.GetRequiredService<Ghost.Core.GhostKernel>();
            var poolOptions = sp.GetRequiredService<IOptions<LinkedInSessionPoolOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Internal.LinkedInSessionPool>>();
            var proxyProvider = sp.GetService<Ghost.Abstractions.IProxyProvider>() ?? Ghost.Proxy.StaticProxyProvider.Empty;
            var linkedInOptions = sp.GetRequiredService<IOptions<LinkedInOptions>>().Value;
            return new Internal.LinkedInSessionPool(kernel, poolOptions, logger, proxyProvider, linkedInOptions);
        });
        // Register platform-specific implementations for core abstractions
        services.AddScoped<Ghost.Abstractions.ITextExtractor, Internal.LinkedInTextExtractor>();
        services.AddScoped<Ghost.Abstractions.ICountryDomainProvider, Internal.LinkedInCountryProvider>();
        // Ensure JsonLdExtractor from Core utilities is available to this platform
        services.AddScoped<Ghost.Abstractions.IJsonLdExtractor, Ghost.Utilities.JsonLdExtractor>();
        // GuestJobSearch implements guest API scraping logic
        services.AddScoped<Internal.IGuestJobSearch, Internal.GuestJobSearch>();
        // Ensure required GhostKernel and IProxyProvider are available for GuestJobSearch
        // GhostKernel is registered by AddGhost and IProxyProvider is registered by host when available
        services.AddScoped<Ghost.Contracts.Social.ISocialClient, LinkedInSocialClient>();
        services.AddScoped<Ghost.Abstractions.IJobScraper, LinkedInJobClient>();
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<LinkedInJobClient>());
        services.AddScoped<Ghost.Contracts.News.INewsClient, LinkedInNewsClient>();
    }
}
