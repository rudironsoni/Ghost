using Ghost.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ghost.Extensions;

/// <summary>
/// Extension methods for registering job queue services
/// </summary>
public static class QueueServiceCollectionExtensions
{
    /// <summary>
    /// Add Redis job queue to the service collection
    /// </summary>
    public static IServiceCollection AddRedisJobQueue(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind configuration
        services.Configure<RedisQueueOptions>(
            configuration.GetSection("Redis"));

        // Register queue service
        services.TryAddSingleton<IJobQueue, RedisJobQueue>();

        return services;
    }

    /// <summary>
    /// Add Redis job queue with custom options
    /// </summary>
    public static IServiceCollection AddRedisJobQueue(
        this IServiceCollection services,
        Action<RedisQueueOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<IJobQueue, RedisJobQueue>();

        return services;
    }
}
