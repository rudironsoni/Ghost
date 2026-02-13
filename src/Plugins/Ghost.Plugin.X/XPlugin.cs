using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.X;

/// <summary>
/// X plugin that wraps the platform extension with plugin metadata and capabilities.
/// </summary>
public sealed class XPlugin : IExtension
{
    private readonly Ghost.Platform.X.XExtension _platformExtension;

    public XPlugin()
    {
        _platformExtension = new Ghost.Platform.X.XExtension();
    }

    /// <inheritdoc />
    public string Name => "X";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => _platformExtension.ProvidedServices;

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => _platformExtension.RequiredServices;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new XPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:X:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:X:RegisterReadinessServices", true),
            RegisterKeyedSocialClient = configuration.GetValue("Ghost:Plugins:X:RegisterKeyedSocialClient", true)
        };

        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

        if (pluginOptions.RegisterReadinessServices)
        {
            // Register plugin-specific services
            services.AddSingleton<XPluginCapabilities>(sp => new XPluginCapabilities
            {
                RequiresBrowser = true,
                RequiresProxy = false,
                SupportsJobs = false,
                SupportsSocial = true,
                SupportsNews = false,
                SupportsSimulation = true
            });

            services.AddSingleton<IXPluginReadinessCheck, XPluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedSocialClient)
        {
            // Register keyed ISocialClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Social.ISocialClient>("x", (sp, _) =>
                sp.GetRequiredService<Ghost.Platform.X.XSocialClient>());
        }
    }
}
