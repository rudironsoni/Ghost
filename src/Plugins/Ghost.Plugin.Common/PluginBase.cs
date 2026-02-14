using Ghost.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.Common;

/// <summary>
/// Base class for Ghost plugins providing common functionality and IExtension implementation.
/// </summary>
public abstract class PluginBase : IExtension
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    public abstract Version Version { get; }

    /// <summary>
    /// Gets the services provided by this plugin.
    /// </summary>
    public virtual IReadOnlyList<Type> ProvidedServices => Array.Empty<Type>();

    /// <summary>
    /// Gets the services required by this plugin.
    /// </summary>
    public virtual IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    /// <summary>
    /// Configures services for the plugin.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Base implementation does nothing.
        // Override in derived classes to register services.
    }
}
