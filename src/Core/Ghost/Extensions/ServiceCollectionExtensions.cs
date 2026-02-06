using Ghost.Abstractions;
using Ghost.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGhostKernel(this IServiceCollection services, Action<Ghost.Core.KernelOptions>? configure = null)
    {
        var options = new Ghost.Core.KernelOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<Ghost.Core.GhostKernel>(sp =>
        {
            var opts = sp.GetRequiredService<Ghost.Core.KernelOptions>();
            return Ghost.Core.GhostKernel.CreateAsync(opts).GetAwaiter().GetResult();
        });

        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory>();
        // Core services
        services.AddSingleton<IDeduplicationService, DeduplicationService>();
        return services;
    }
}
