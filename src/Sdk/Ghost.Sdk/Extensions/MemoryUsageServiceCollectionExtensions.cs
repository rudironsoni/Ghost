using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Extension methods for registering memory usage monitoring services.
/// </summary>
public static class MemoryUsageServiceCollectionExtensions
{
    /// <summary>
    /// Adds memory usage monitoring services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method registers the <see cref="IMemoryUsageExtension"/> and <see cref="MemoryUsageExtension"/>
    /// as scoped services. It also registers <see cref="MemoryOptions"/> as a configurable option.
    /// </remarks>
    public static IServiceCollection AddMemoryUsageMonitoring(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IMemoryUsageExtension, MemoryUsageExtension>();
        services.TryAddScoped<MemoryUsageExtension>();

        return services;
    }

    /// <summary>
    /// Adds memory usage monitoring services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure the memory options.</param>
    /// <returns>The service collection so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method registers the <see cref="IMemoryUsageExtension"/> and <see cref="MemoryUsageExtension"/>
    /// as scoped services. It also configures <see cref="MemoryOptions"/> using the provided action.
    /// </remarks>
    public static IServiceCollection AddMemoryUsageMonitoring(
        this IServiceCollection services,
        Action<MemoryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        return services.AddMemoryUsageMonitoring();
    }

    /// <summary>
    /// Adds memory usage monitoring services with configuration from an <see cref="IConfiguration"/> instance.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method registers the <see cref="IMemoryUsageExtension"/> and <see cref="MemoryUsageExtension"/>
    /// as scoped services. It also configures <see cref="MemoryOptions"/> from the provided configuration section.
    /// The configuration section should be named "Memory".
    /// </remarks>
    public static IServiceCollection AddMemoryUsageMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<MemoryOptions>(configuration.GetSection("Memory"));
        return services.AddMemoryUsageMonitoring();
    }
}
