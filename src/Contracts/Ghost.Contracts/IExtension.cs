using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Contracts;

/// <summary>
/// Represents an extension that can be registered in the Ghost host.
/// Extensions declare their provided and required services and participate in service configuration.
/// </summary>
public interface IExtension
{
    /// <summary>
    /// The human-readable name of the extension.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The extension version.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Services this extension provides.
    /// </summary>
    IReadOnlyList<Type> ProvidedServices { get; }

    /// <summary>
    /// Services this extension requires to operate.
    /// </summary>
    IReadOnlyList<Type> RequiredServices { get; }

    /// <summary>
    /// Configure services for this extension using the provided service collection and configuration.
    /// </summary>
    /// <param name="services">Service collection to register services into.</param>
    /// <param name="configuration">Configuration to read options from.</param>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
