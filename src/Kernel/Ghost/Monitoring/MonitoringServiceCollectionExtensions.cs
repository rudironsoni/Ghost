using Ghost.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Monitoring;

/// <summary>
/// Dependency injection registration for monitoring services.
/// </summary>
public static class MonitoringServiceCollectionExtensions
{
    /// <summary>
    /// Adds monitoring services.
    /// </summary>
    public static IServiceCollection AddGhostMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;

        services.AddSingleton<MetricsService>();
        services.AddScoped<IHealthReportService, HealthReportService>();
        return services;
    }

    /// <summary>
    /// Adds Redis queue services including IJobDispatcher.
    /// </summary>
    public static IServiceCollection AddRedisQueue(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RedisQueueOptions>(configuration.GetSection("RedisQueue"));
        services.AddSingleton<IJobDispatcher, RedisJobDispatcher>();

        return services;
    }
}
