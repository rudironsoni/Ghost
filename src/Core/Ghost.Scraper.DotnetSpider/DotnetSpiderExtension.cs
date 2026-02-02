using System;
using System.Collections.Generic;
using Ghost.Contracts;
using Ghost.Platform.Common.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Scraper.DotnetSpider;

/// <summary>
/// Registers the DotnetSpider scraping framework extension.
/// </summary>
public sealed class DotnetSpiderExtension : IExtension
{
    /// <summary>
    /// Gets the human-readable name for the extension.
    /// </summary>
    public string Name => "DotnetSpider";

    /// <summary>
    /// Gets the version of the extension.
    /// </summary>
    public Version Version => new(1, 0, 0);

    /// <summary>
    /// Gets the services provided by this extension.
    /// </summary>
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(DotnetSpiderOptions), typeof(DotnetSpiderGhostAdapter) };

    /// <summary>
    /// Gets the services required by this extension.
    /// </summary>
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(ISessionOrchestrator) };

    /// <summary>
    /// Configures services for the DotnetSpider integration.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <param name="configuration">The configuration root for binding options.</param>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DotnetSpiderOptions>(configuration.GetSection("Ghost:Extensions:DotnetSpider"));
        services.AddScoped<DotnetSpiderGhostAdapter>();
    }
}