using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGhostwriterKernel(this IServiceCollection services, Action<Ghostwright.Core.KernelOptions>? configure = null)
    {
        var options = new Ghostwright.Core.KernelOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<Ghostwright.Core.GhostwriterKernel>(sp =>
        {
            var opts = sp.GetRequiredService<Ghostwright.Core.KernelOptions>();
            return Ghostwright.Core.GhostwriterKernel.CreateAsync(opts).GetAwaiter().GetResult();
        });

        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory>();
        return services;
    }
}
