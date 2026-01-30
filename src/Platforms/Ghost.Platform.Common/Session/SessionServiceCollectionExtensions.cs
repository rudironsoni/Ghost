using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ghost.Abstractions;

namespace Ghost.Platform.Common.Session;

/// <summary>
/// Extension methods for registering session services
/// </summary>
public static class SessionServiceCollectionExtensions
{
    /// <summary>
    /// Register RotatingProxySession services with default options
    /// </summary>
    public static IServiceCollection AddRotatingProxySession(this IServiceCollection services, RotatingProxySessionOptions? options = null)
    {
        // IProxyProvider must be registered separately by the consuming platform
        
        if (options != null)
        {
            services.AddSingleton(options);
        }
        else
        {
            services.AddSingleton(new RotatingProxySessionOptions());
        }
        
        services.AddScoped<SessionFactory>();
        services.AddScoped<RotatingProxySession>(provider =>
        {
            var proxyProvider = provider.GetRequiredService<IProxyProvider>();
            var sessionOptions = provider.GetService<RotatingProxySessionOptions>() ?? new RotatingProxySessionOptions();
            return new RotatingProxySession(proxyProvider, sessionOptions);
        });
        
        return services;
    }

    /// <summary>
    /// Register RotatingProxySession services with custom configuration
    /// </summary>
    public static IServiceCollection AddRotatingProxySession(this IServiceCollection services, Action<RotatingProxySessionOptions> configureOptions)
    {
        var options = new RotatingProxySessionOptions();
        configureOptions(options);
        
        return services.AddRotatingProxySession(options);
    }

    /// <summary>
    /// Register platform-specific session services
    /// </summary>
    public static IServiceCollection AddPlatformSession(this IServiceCollection services, string platformName)
    {
        services.AddRotatingProxySession(options =>
        {
            ConfigurePlatformOptions(options, platformName);
        });
        
        return services;
    }

    /// <summary>
    /// Configure platform-specific options
    /// </summary>
    private static void ConfigurePlatformOptions(RotatingProxySessionOptions options, string platformName)
    {
        options.EnableProxyRotation = true;
        options.EnableTlsFingerprinting = true;
        options.UseCookies = true;

        switch (platformName.ToLowerInvariant())
        {
            case "glassdoor":
                options.Timeout = TimeSpan.FromSeconds(45);
                options.MaxRetries = 5;
                options.JitterMinMs = 2000;
                options.JitterMaxMs = 8000;
                break;
            
            case "indeed":
                options.Timeout = TimeSpan.FromSeconds(30);
                options.MaxRetries = 3;
                options.JitterMinMs = 1000;
                options.JitterMaxMs = 4000;
                break;
            
            case "google":
                options.Timeout = TimeSpan.FromSeconds(60);
                options.MaxRetries = 4;
                options.JitterMinMs = 3000;
                options.JitterMaxMs = 10000;
                break;
            
            default:
                // Use default options for unknown platforms
                break;
        }
    }
}