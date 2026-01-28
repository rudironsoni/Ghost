using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Ghost.Core;

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

        // Register GhostKernel as singleton created from KernelOptions
        _services.AddSingleton(provider =>
        {
            var opts = provider.GetRequiredService<IOptions<KernelOptions>>().Value;
            // Create synchronously since DI registration is not async
            return GhostKernel.CreateAsync(opts).GetAwaiter().GetResult();
        });

        // Register IBrowserSession as a factory from the kernel
            _services.AddScoped<IBrowserSession>(provider =>
            {
                var kernel = provider.GetRequiredService<GhostKernel>();
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

            // Tell the extension loader that IBrowserSession is provided by the kernel
            var kernelProvidedServices = new HashSet<Type> { typeof(IBrowserSession) };
            ExtensionLoader.LoadExtensions(_extensions, _services, _configuration, kernelProvidedServices);
        }
    }
}
