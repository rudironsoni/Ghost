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
    /// Registers core Ghost Kernel services without initializing the kernel.
    /// For proper async kernel initialization, use Ghost.Hosting.GhostKernelManager with IHostedService.
    /// </summary>
    public static IServiceCollection AddGhostKernelServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeduplicationService, DeduplicationService>();
        return services;
    }
}
