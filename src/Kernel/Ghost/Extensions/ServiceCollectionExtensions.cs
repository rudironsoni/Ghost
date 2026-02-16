using Ghost.Kernel;
using Ghost.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Extensions;

/// <summary>
/// Extension methods for configuring Ghost Kernel services in an <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// <para><strong>ConfigureAwait Policy:</strong></para>
/// <para>
/// All library code awaits MUST use <c>ConfigureAwait(false)</c> to prevent deadlocks in environments
/// with synchronization contexts (e.g., WinForms, WPF, ASP.NET Classic). This is not required for
/// application code (WebAPI, Worker) or test code.
/// </para>
/// <para>
/// Blocking calls like <c>.Result</c> or <c>.Wait()</c> are prohibited in library code.
/// Use proper async/await patterns instead.
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers GhostKernel services. Note: This method performs synchronous initialization which should be avoided.
    /// For proper async initialization, use Ghost.Hosting.GhostKernelManager with IHostedService.
    /// </summary>
    /// <remarks>
    /// This method uses <c>GetAwaiter().GetResult()</c> which is a blocking call. This is acceptable
    /// here because:
    /// <list type="bullet">
    ///   <item>The method is marked <see cref="Obsolete"/></item>
    ///   <item>It's used within a <see cref="Lazy{T}"/> factory where async/await is not possible</item>
    ///   <item>The recommended alternative (GhostKernelManager) provides true async initialization</item>
    /// </list>
    /// </remarks>
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
                // Blocking call is necessary here because Lazy<T> factory must be synchronous.
                // The method is obsolete; use GhostKernelManager for proper async initialization.
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
