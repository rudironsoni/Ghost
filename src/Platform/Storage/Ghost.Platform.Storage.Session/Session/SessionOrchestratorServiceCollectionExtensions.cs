using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Storage.Session;

/// <summary>
/// Extension methods for registering SessionOrchestrator services
/// </summary>
public static class SessionOrchestratorServiceCollectionExtensions
{
    /// <summary>
    /// Registers SessionOrchestrator services with default options.
    /// Requires IProxyProvider and ITieredBrowserPool to be registered separately.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSessionOrchestrator(this IServiceCollection services)
    {
        return services.AddSessionOrchestrator(options => { });
    }

    /// <summary>
    /// Registers SessionOrchestrator services with configuration.
    /// Requires IProxyProvider and ITieredBrowserPool to be registered separately.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure SessionOrchestratorOptions</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSessionOrchestrator(
        this IServiceCollection services,
        Action<SessionOrchestratorOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.AddSingleton<IValidateOptions<SessionOrchestratorOptions>, SessionOrchestratorOptionsValidator>();
        services.TryAddSingleton<ISessionOrchestrator, SessionOrchestrator>();

        return services;
    }

    /// <summary>
    /// Registers SessionOrchestrator services with pre-configured options instance.
    /// Requires IProxyProvider and ITieredBrowserPool to be registered separately.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">Pre-configured SessionOrchestratorOptions instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSessionOrchestrator(
        this IServiceCollection services,
        SessionOrchestratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();
        services.AddSingleton(Options.Create(options));
        services.TryAddSingleton<ISessionOrchestrator, SessionOrchestrator>();

        return services;
    }
}

/// <summary>
/// Validates SessionOrchestratorOptions on DI registration
/// </summary>
internal sealed class SessionOrchestratorOptionsValidator : IValidateOptions<SessionOrchestratorOptions>
{
    public ValidateOptionsResult Validate(string? name, SessionOrchestratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail($"SessionOrchestratorOptions validation failed: {ex.Message}");
        }
    }
}
