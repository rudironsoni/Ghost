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
        var pluginOptions = new LinkedInPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:LinkedIn:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:LinkedIn:RegisterReadinessServices", true),
            RegisterKeyedJobClient = configuration.GetValue("Ghost:Plugins:LinkedIn:RegisterKeyedJobClient", true)
        };

        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

        if (pluginOptions.RegisterReadinessServices)
        {
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
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedJobClient)
        {
            // Register keyed IJobClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("linkedin", (sp, _) =>
                sp.GetRequiredService<Ghost.Platform.LinkedIn.LinkedInJobClient>());
        }
    }
}
