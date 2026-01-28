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

        // Try to get an IConfiguration already registered, otherwise create a basic one
        IConfiguration? configuration = null;
        try
        {
            var provider = services.BuildServiceProvider(validateScopes: true);
            configuration = provider.GetService<IConfiguration>();
        }
        catch
        {
            // ignore provider build failures; fall back to environment configuration
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
        configure(builder);
        builder.Build();

        return services;
    }
}
