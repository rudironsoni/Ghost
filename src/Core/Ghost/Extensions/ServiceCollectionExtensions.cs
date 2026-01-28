using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGhostwriterKernel(this IServiceCollection services, Action<Ghost.Core.KernelOptions>? configure = null)
    {
        var options = new Ghost.Core.KernelOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<Ghost.Core.GhostwriterKernel>(sp =>
        {
            var opts = sp.GetRequiredService<Ghost.Core.KernelOptions>();
            return Ghost.Core.GhostwriterKernel.CreateAsync(opts).GetAwaiter().GetResult();
        });

        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory>();
        return services;
    }
}
