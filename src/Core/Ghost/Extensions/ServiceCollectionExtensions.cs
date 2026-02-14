using Ghost.Abstractions;
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
    public static IServiceCollection AddGhostKernel(this IServiceCollection services, Action<Ghost.Core.KernelOptions>? configure = null)
    {
        var options = new Ghost.Core.KernelOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Use Lazy<T> to defer initialization until first use
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<Ghost.Core.KernelOptions>();
            return new Lazy<Ghost.Core.GhostKernel>(() =>
            {
                // This will be executed on first access, not during DI registration
                return Ghost.Core.GhostKernel.CreateAsync(opts).GetAwaiter().GetResult();
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        });

        // Register the kernel itself - resolved from Lazy
        services.AddSingleton(sp => sp.GetRequiredService<Lazy<Ghost.Core.GhostKernel>>().Value);

        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory>();
        // Core services
        services.AddSingleton<IDeduplicationService, DeduplicationService>();
        return services;
    }
}
