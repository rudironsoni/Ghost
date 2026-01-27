using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Ghostwright.Core;

namespace Ghostwright.Hosting;

/// <summary>
/// Builder used to configure Ghostwright hosting and extensions.
/// </summary>
public sealed class GhostwriterBuilder
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly List<IExtension> _extensions = new();
    private Action<KernelOptions>? _kernelConfigure;

    internal GhostwriterBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Configure kernel options used to create the <see cref="GhostwriterKernel"/>.
    /// </summary>
    /// <param name="configure">Configure action for <see cref="KernelOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public GhostwriterBuilder ConfigureKernel(Action<KernelOptions> configure)
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
    public GhostwriterBuilder UseExtension<TExtension>() where TExtension : IExtension, new()
    {
        return UseExtension(new TExtension());
    }

    /// <summary>
    /// Add an extension instance.
    /// </summary>
    /// <param name="extension">Extension instance to add.</param>
    /// <returns>The builder for chaining.</returns>
    public GhostwriterBuilder UseExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        _extensions.Add(extension);
        return this;
    }

    internal void Build()
    {
        // Register default options
        _services.Configure<GhostwriterOptions>(opts => { });

        // Ensure KernelOptions is available (caller may have configured via ConfigureKernel already)
        _services.Configure<KernelOptions>(opts => { });

        // Register GhostwriterKernel as singleton created from KernelOptions
        _services.AddSingleton(provider =>
        {
            var opts = provider.GetRequiredService<IOptions<KernelOptions>>().Value;
            // Create synchronously since DI registration is not async
            return GhostwriterKernel.CreateAsync(opts).GetAwaiter().GetResult();
        });

        // Load extensions via loader (validates and registers)
            var loader = new ExtensionLoader();
            if (_extensions.Count > 0)
            {
            if (_services.Any(sd => sd.ServiceType == typeof(GhostwriterOptions)))
            {
                // nothing special
            }

            if (_services is null) throw new InvalidOperationException("Services collection is missing");

            ExtensionLoader.LoadExtensions(_extensions, _services, _configuration);
        }
    }
}
