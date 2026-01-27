using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ghostwright.Hosting;

/// <summary>
/// Service collection extensions to add Ghostwright hosting to an application.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ghostwright hosting with the provided configure action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Builder configure callback.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddGhostwright(
        this IServiceCollection services,
        Action<GhostwriterBuilder> configure)
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

        return services.AddGhostwright(configuration, configure);
    }

    /// <summary>
    /// Adds Ghostwright hosting with explicit configuration instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration instance to use.</param>
    /// <param name="configure">Builder configure callback.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddGhostwright(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<GhostwriterBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new GhostwriterBuilder(services, configuration);
        configure(builder);
        builder.Build();

        return services;
    }
}
