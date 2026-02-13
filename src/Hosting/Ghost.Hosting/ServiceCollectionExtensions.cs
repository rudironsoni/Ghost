using Ghost.Engine.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ghost.Hosting;

/// <summary>
/// Service collection extensions to add Ghost hosting to an application.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ghost hosting with the provided configure action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Builder configure callback.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddGhost(
        this IServiceCollection services,
        Action<GhostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Try to get an IConfiguration already registered, without building the provider
        IConfiguration? configuration = null;
        var configDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConfiguration));
        if (configDescriptor?.ImplementationInstance is IConfiguration existingConfig)
        {
            configuration = existingConfig;
        }

        // Create a basic configuration if none available
        configuration ??= new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();

        return services.AddGhost(configuration, configure);
    }

    /// <summary>
    /// Adds Ghost hosting with explicit configuration instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration instance to use.</param>
    /// <param name="configure">Builder configure callback.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddGhost(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<GhostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new GhostBuilder(services, configuration);
        services.AddGhostEngineHosting(configuration);
        configure(builder);
        builder.Build();

        return services;
    }
}
