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
        services.AddSingleton<IHealthReportService, HealthReportService>();
        return services;
    }
}
