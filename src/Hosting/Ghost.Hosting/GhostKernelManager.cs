using Ghost.Kernel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ghost.Hosting;

/// <summary>
/// Manages asynchronous kernel initialization and lifecycle.
/// </summary>
internal sealed class GhostKernelManager : IGhostKernel, IHostedService, IAsyncDisposable
{
    private readonly KernelOptions _options;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private Task<GhostKernel>? _kernelTask;

    public GhostKernelManager(IOptions<KernelOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = await GetKernelAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_kernelTask is null)
        {
            return;
        }

        GhostKernel kernel = await _kernelTask.ConfigureAwait(false);
        await kernel.DisposeAsync().ConfigureAwait(false);
    }

    public async Task<IBrowserSession> NewSessionAsync(SessionOptions? options = null, CancellationToken ct = default)
    {
        GhostKernel kernel = await GetKernelAsync(ct).ConfigureAwait(false);
        return await kernel.NewSessionAsync(options, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_kernelTask is not null)
        {
            GhostKernel kernel = await _kernelTask.ConfigureAwait(false);
            await kernel.DisposeAsync().ConfigureAwait(false);
        }

        _initializationGate.Dispose();
    }

    private async Task<GhostKernel> GetKernelAsync(CancellationToken cancellationToken)
    {
        if (_kernelTask is not null)
        {
            return await _kernelTask.ConfigureAwait(false);
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _kernelTask ??= GhostKernel.CreateAsync(_options, cancellationToken);
        }
        finally
        {
            _initializationGate.Release();
        }

        return await _kernelTask.ConfigureAwait(false);
    }
}
