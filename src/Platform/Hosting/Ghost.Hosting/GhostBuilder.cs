using Ghost.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Hosting;

/// <summary>
/// Builder used to configure Ghost hosting and extensions.
/// </summary>
public sealed class GhostBuilder
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly List<IExtension> _extensions = [];
    private Action<KernelOptions>? _kernelConfigure;

    internal GhostBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Configure kernel options used to create the <see cref="GhostKernel"/>.
    /// </summary>
    /// <param name="configure">Configure action for <see cref="KernelOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public GhostBuilder ConfigureKernel(Action<KernelOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _kernelConfigure = configure;
        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Add an extension type.
    /// </summary>
    /// <typeparam name="TExtension">Extension type (must have parameterless constructor).</typeparam>
    /// <returns>The builder for chaining.</returns>
    public GhostBuilder UseExtension<TExtension>() where TExtension : IExtension, new()
    {
        return UseExtension(new TExtension());
    }

    /// <summary>
    /// Add an extension instance.
    /// </summary>
    /// <param name="extension">Extension instance to add.</param>
    /// <returns>The builder for chaining.</returns>
    public GhostBuilder UseExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        _extensions.Add(extension);
        return this;
    }

    internal void Build()
    {
        // Register default options
        _services.Configure<GhostOptions>(opts => { });

        // Ensure KernelOptions is available (caller may have configured via ConfigureKernel already)
        _services.Configure<KernelOptions>(opts => { });

        // Register core services needed by extensions
        _services.AddSingleton<Ghost.IDeduplicationService, Ghost.Utilities.DeduplicationService>();

        // Register kernel manager and expose it as IGhostKernel.
        _services.AddSingleton<GhostKernelManager>();
        _services.AddSingleton<IGhostKernel>(provider => provider.GetRequiredService<GhostKernelManager>());
        _services.AddHostedService(provider => provider.GetRequiredService<GhostKernelManager>());

        // Register scoped browser session wrapper that initializes asynchronously on first use.
        _services.AddScoped<IBrowserSession, DeferredBrowserSession>();

        // Load extensions via loader (validates and registers)
        var loader = new ExtensionLoader();
        if (_extensions.Count > 0)
        {
            if (_services.Any(sd => sd.ServiceType == typeof(GhostOptions)))
            {
                // nothing special
            }

            if (_services is null) throw new InvalidOperationException("Services collection is missing");

            // Tell the extension loader which kernel services are available.
            var kernelProvidedServices = new HashSet<Type> { typeof(IBrowserSession), typeof(IGhostKernel) };
            ExtensionLoader.LoadExtensions(_extensions, _services, _configuration, kernelProvidedServices);
        }
    }
}
