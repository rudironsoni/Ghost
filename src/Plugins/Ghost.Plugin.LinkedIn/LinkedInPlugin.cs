using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.LinkedIn;

/// <summary>
/// LinkedIn plugin that wraps the platform extension with plugin metadata and capabilities.
/// </summary>
public sealed class LinkedInPlugin : IExtension
{
    private readonly Ghost.Platform.LinkedIn.LinkedInExtension _platformExtension;

    public LinkedInPlugin()
    {
        _platformExtension = new Ghost.Platform.LinkedIn.LinkedInExtension();
    }

    /// <inheritdoc />
    public string Name => "LinkedIn";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => _platformExtension.ProvidedServices;

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => _platformExtension.RequiredServices;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

        // Register plugin-specific services
        services.AddSingleton<LinkedInPluginCapabilities>(sp => new LinkedInPluginCapabilities
        {
            RequiresBrowser = true,
            RequiresProxy = false,
            SupportsJobs = true,
            SupportsSocial = true,
            SupportsNews = true
        });

        services.AddSingleton<ILinkedInPluginReadinessCheck, LinkedInPluginReadinessCheck>();

        // Register keyed IJobClient mapping for worker compatibility
        // This allows workers to resolve IJobClient by key "linkedin"
        services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("linkedin", (sp, _) =>
            sp.GetRequiredService<Ghost.Platform.LinkedIn.LinkedInJobClient>());
    }
}
