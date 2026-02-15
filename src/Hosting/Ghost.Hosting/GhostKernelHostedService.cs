using Ghost.Kernel;
using Microsoft.Extensions.Hosting;

namespace Ghost.Hosting;

/// <summary>
/// Managed hosted service to ensure GhostKernel is properly disposed on application shutdown.
/// </summary>
public class GhostKernelHostedService : IHostedService
{
    private readonly GhostKernel _kernel;
    private readonly IHostApplicationLifetime _lifetime;

    public GhostKernelHostedService(GhostKernel kernel, IHostApplicationLifetime lifetime)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));

        // Register for application stopping as a fail-safe shutdown hook
        _lifetime.ApplicationStopping.Register(OnStopping);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Kernel is lazy-loaded or already initialized by DI factory,
        // we just need to ensure we are hooked for shutdown.
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // DisposeAsync will handle the cleanup of browser and playwright processes
        return _kernel.DisposeAsync().AsTask();
    }

    private void OnStopping()
    {
        // Ensure synchronous disposal happens if StopAsync isn't called or completes too late
        // This blocks the shutdown thread until cleanup is done, which is what we want
        // to prevent orphaned processes.
        try
        {
            _kernel.Dispose();
        }
        catch
        {
            // Ignore errors during shutdown to avoid crashing the unhandled exception handler
        }
    }
}
