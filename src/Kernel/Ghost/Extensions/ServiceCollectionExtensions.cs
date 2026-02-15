using Ghost.Abstractions;
using Ghost.Kernel;
using Ghost.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers GhostKernel services. Note: This method performs synchronous initialization which should be avoided.
    /// For proper async initialization, use Ghost.Hosting.GhostKernelManager with IHostedService.
    /// </summary>
    [Obsolete("Use Ghost.Hosting.GhostKernelManager for async initialization instead. This method will be removed in a future version.")]
    public static IServiceCollection AddGhostKernel(this IServiceCollection services, Action<Ghost.Kernel.KernelOptions>? configure = null)
    {
        var options = new Ghost.Kernel.KernelOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Use Lazy<T> to defer initialization until first use
        services.AddSingleton(sp =>
        {
            KernelOptions opts = sp.GetRequiredService<Ghost.Kernel.KernelOptions>();
            return new Lazy<Ghost.Kernel.GhostKernel>(() =>
            {
                // This will be executed on first access, not during DI registration
                return Ghost.Kernel.GhostKernel.CreateAsync(opts).GetAwaiter().GetResult();
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        });

        // Register the kernel itself - resolved from Lazy
        services.AddSingleton(sp => sp.GetRequiredService<Lazy<Ghost.Kernel.GhostKernel>>().Value);

        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory>();
        // Core services
        services.AddSingleton<IDeduplicationService, DeduplicationService>();
        return services;
    }
}
