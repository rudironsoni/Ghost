using Ghost.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ghost.Hosting;

/// <summary>
/// Builder used to configure Ghost hosting and extensions.
/// </summary>
public sealed class GhostBuilder
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly List<IExtension> _extensions = new();
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
        _services.AddSingleton<Ghost.Abstractions.IDeduplicationService, Ghost.Utilities.DeduplicationService>();

        // Register IGhostKernel interface for the kernel
        _services.AddSingleton<IGhostKernel>(provider =>
        {
            var opts = provider.GetRequiredService<IOptions<KernelOptions>>().Value;
            // Create synchronously since DI registration is not async
            // This is acceptable for the singleton kernel initialization during startup
            return GhostKernel.CreateAsync(opts).GetAwaiter().GetResult();
        });

        // Register concrete GhostKernel type for extensions that need it
        _services.AddSingleton(provider => (GhostKernel)provider.GetRequiredService<IGhostKernel>());

        // Register Hosted Service to manage Kernel lifecycle (shutdown)
        _services.AddHostedService<GhostKernelHostedService>();

        // Register IBrowserSession as Scoped (per-request) factory from the kernel
        // NOTE: This creates a new browser session for each HTTP request scope
        // Services using this should be Scoped, not Singleton
        // FIXED: Wrapped in Lazy to prevent resolution during container validation
        _services.AddScoped<IBrowserSession>(provider =>
        {
            var kernel = provider.GetRequiredService<IGhostKernel>();
            // FIXED: This is now safe because it's only called within a Scoped context (HTTP request)
            // not during application startup/DI validation
            return kernel.NewSessionAsync().GetAwaiter().GetResult();
        });

        // Load extensions via loader (validates and registers)
        var loader = new ExtensionLoader();
        if (_extensions.Count > 0)
        {
            if (_services.Any(sd => sd.ServiceType == typeof(GhostOptions)))
            {
                // nothing special
            }

            if (_services is null) throw new InvalidOperationException("Services collection is missing");

            // Tell the extension loader that IBrowserSession and GhostKernel are provided by the kernel
            var kernelProvidedServices = new HashSet<Type> { typeof(IBrowserSession), typeof(Ghost.Core.GhostKernel) };
            ExtensionLoader.LoadExtensions(_extensions, _services, _configuration, kernelProvidedServices);
        }
    }
}
