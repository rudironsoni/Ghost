using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Hosting;

/// <summary>
/// Defines a hosting extension that can register services and declare
/// dependencies on services provided by other extensions.
/// </summary>
public interface IExtension
{
    /// <summary>
    /// Gets the extension friendly name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the service types that this extension requires from other extensions.
    /// </summary>
    IReadOnlyList<Type> RequiredServices { get; }

    /// <summary>
    /// Gets the service types that this extension provides to others.
    /// </summary>
    IReadOnlyList<Type> ProvidedServices { get; }

    /// <summary>
    /// Called to register services for this extension.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Application configuration.</param>
    void Register(IServiceCollection services, IConfiguration configuration);
}
