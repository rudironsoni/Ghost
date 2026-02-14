using Ghost.Contracts.Simulation;
using Ghost.Contracts.Social;
using Ghost.Core;
using Ghost.Plugin.X.Configuration;
using Ghost.Plugin.X.Exceptions;
using Ghost.Plugin.X.Internal;
using Ghost.Plugin.X.MultiAccount;
using Ghost.Plugin.X.Performance;
using Ghost.Plugin.X.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.X;

/// <summary>
/// Extension for integrating X (Twitter) platform with Ghost.
/// </summary>
public class XExtension : Ghost.Contracts.IExtension
{
    /// <inheritdoc />
    public string Name => "Ghost.Platform.X";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => new[]
    {
        typeof(ISocialClient),
        typeof(IXPlatformSimulationValidator),
        typeof(IXMetricsService),
        typeof(IXWebhookService),
        typeof(IXAccountManager)
    };

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => new[]
    {
        typeof(IBrowserSession)
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Bind from nested path: Ghost:Extensions:X in appsettings.json
        services.Configure<XOptions>(configuration.GetSection("Ghost:Extensions:X"));
        services.Configure<BrowserSessionPoolOptions>(configuration.GetSection("Ghost:Extensions:X:Pool"));

        // Register configuration validation
        services.AddSingleton<IValidateOptions<XOptions>, XConfigurationValidator>();
        services.AddSingleton<XPlatformHealthCheck>();

        // Register performance services
        services.AddSingleton<IBrowserSessionPool, BrowserSessionPool>();
        services.AddSingleton<BrowserSessionPoolOptions>(sp =>
        {
            var options = sp.GetService<IOptions<BrowserSessionPoolOptions>>()?.Value ?? new BrowserSessionPoolOptions();
            return options;
        });

        // Register metrics and webhooks
        services.AddSingleton<IXMetricsService, XMetricsService>();
        services.AddSingleton<IXWebhookService, XWebhookService>();

        // Register multi-account support
        services.AddSingleton<IXAccountManager, XAccountManager>();

        // Register internal services
        services.AddSingleton<XPostContentSplitter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<XOptions>>();
            return new XPostContentSplitter(options.Value.MaxTweetLength);
        });

        services.AddScoped<XAuthenticator>();
        services.AddScoped<XThreadComposer>();
        services.AddScoped<XSimulationValidator>();

        // Register the main social client
        services.AddScoped<ISocialClient, XSocialClient>();

        // Register the simulation validator as IXPlatformSimulationValidator
        services.AddScoped<IXPlatformSimulationValidator>(sp =>
            sp.GetRequiredService<XSimulationValidator>());
    }
}

/// <summary>
/// Service collection extensions for X platform.
/// </summary>
public static class XServiceCollectionExtensions
{
    /// <summary>
    /// Adds X platform services to the service collection.
    /// </summary>
    public static IServiceCollection AddXPlatform(this IServiceCollection services, Action<XOptions>? configureOptions = null)
    {
        // Register XOptions with defaults
        services.AddOptions<XOptions>().ValidateOnStart();
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Register configuration validation
        services.AddSingleton<IValidateOptions<XOptions>, XConfigurationValidator>();
        services.AddSingleton<XPlatformHealthCheck>();

        // Register performance services
        services.AddSingleton<BrowserSessionPoolOptions>();
        services.AddSingleton<IBrowserSessionPool, BrowserSessionPool>();

        // Register metrics and webhooks
        services.AddSingleton<IXMetricsService, XMetricsService>();
        services.AddSingleton<IXWebhookService, XWebhookService>();

        // Register multi-account support
        services.AddSingleton<IXAccountManager, XAccountManager>();

        // Register internal services
        services.AddSingleton<XPostContentSplitter>(sp =>
        {
            var options = sp.GetService<IOptions<XOptions>>()?.Value ?? new XOptions();
            return new XPostContentSplitter(options.MaxTweetLength);
        });

        services.AddScoped<XAuthenticator>();
        services.AddScoped<XThreadComposer>();
        services.AddScoped<XSimulationValidator>();
        services.AddScoped<ISocialClient, XSocialClient>();
        services.AddScoped<IXPlatformSimulationValidator>(sp =>
            sp.GetRequiredService<XSimulationValidator>());

        return services;
    }

    /// <summary>
    /// Adds X platform with configuration from IConfiguration.
    /// </summary>
    public static IServiceCollection AddXPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<XOptions>(configuration.GetSection("X"));
        services.Configure<BrowserSessionPoolOptions>(configuration.GetSection("X:Pool"));

        // Register configuration validation
        services.AddSingleton<IValidateOptions<XOptions>, XConfigurationValidator>();
        services.AddSingleton<XPlatformHealthCheck>();

        // Register performance services
        services.AddSingleton<IBrowserSessionPool, BrowserSessionPool>();

        // Register metrics and webhooks
        services.AddSingleton<IXMetricsService, XMetricsService>();
        services.AddSingleton<IXWebhookService, XWebhookService>();

        // Register multi-account support
        services.AddSingleton<IXAccountManager, XAccountManager>();

        // Register internal services
        services.AddSingleton<XPostContentSplitter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<XOptions>>();
            return new XPostContentSplitter(options.Value.MaxTweetLength);
        });

        services.AddScoped<XAuthenticator>();
        services.AddScoped<XThreadComposer>();
        services.AddScoped<XSimulationValidator>();
        services.AddScoped<ISocialClient, XSocialClient>();
        services.AddScoped<IXPlatformSimulationValidator>(sp =>
            sp.GetRequiredService<XSimulationValidator>());

        return services;
    }

    /// <summary>
    /// Adds X platform with health checks.
    /// </summary>
    public static IServiceCollection AddXPlatformWithHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddXPlatform(configuration);
        services.AddHostedService<XHealthCheckService>();
        return services;
    }

    /// <summary>
    /// Configures multiple X accounts for rotation.
    /// </summary>
    public static IServiceCollection ConfigureXAccounts(this IServiceCollection services, IEnumerable<XAccountOptions> accounts)
    {
        services.AddSingleton<IEnumerable<XAccountOptions>>(accounts);

        services.AddSingleton<IXAccountManager>(sp =>
        {
            var manager = new XAccountManager(sp.GetRequiredService<ILogger<XAccountManager>>());
            var accountOptions = sp.GetRequiredService<IEnumerable<XAccountOptions>>();

            foreach (var account in accountOptions)
            {
                manager.RegisterAccount(account.AccountId, account);
            }

            return manager;
        });

        return services;
    }
}

/// <summary>
/// Background service for health checks.
/// </summary>
public partial class XHealthCheckService : BackgroundService
{
    private readonly XPlatformHealthCheck _healthCheck;
    private readonly IXMetricsService _metrics;
    private readonly ILogger<XHealthCheckService> _logger;

    public XHealthCheckService(
        XPlatformHealthCheck healthCheck,
        IXMetricsService metrics,
        ILogger<XHealthCheckService> logger)
    {
        _healthCheck = healthCheck;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _healthCheck.CheckHealthAsync(stoppingToken);

                if (result.Status == HealthStatus.Unhealthy)
                {
                    Log.HealthCheckFailed(_logger,
                        string.Join(", ", result.Messages));
                }
                else if (result.Status == HealthStatus.Degraded)
                {
                    Log.HealthCheckDegraded(_logger,
                        string.Join(", ", result.Messages));
                }

                // Log metrics periodically
                var metrics = _metrics.GetMetrics();
                if (metrics.TotalRequests > 0)
                {
                    Log.Metrics(_logger,
                        metrics.TotalRequests,
                        metrics.SuccessRate,
                        metrics.RateLimitHits);
                }
            }
            catch (Exception ex)
            {
                Log.HealthCheckServiceError(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
