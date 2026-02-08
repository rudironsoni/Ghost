using Ghost.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Extensions;

/// <summary>
/// Extension methods for registering session management services.
/// </summary>
public static class SessionServiceCollectionExtensions
{
    /// <summary>
    /// Add session management services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGhostSessionManagement(
        this IServiceCollection services,
        Action<SessionManagerOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SessionManagerOptions>(options => { });
        }

        services.AddSingleton<ISessionManager, SessionManager>();

        return services;
    }
}
