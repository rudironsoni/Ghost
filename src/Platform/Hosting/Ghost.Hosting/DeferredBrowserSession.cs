using Microsoft.Playwright;

namespace Ghost.Hosting;

/// <summary>
/// Scoped browser session wrapper that creates the underlying session on first use.
/// </summary>
internal sealed class DeferredBrowserSession : Ghost.IBrowserSession
{
    private readonly Ghost.Kernel.IGhostKernel _kernel;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private Task<Ghost.IBrowserSession>? _sessionTask;
    private Ghost.IBrowserSession? _session;

    public DeferredBrowserSession(Ghost.Kernel.IGhostKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        _kernel = kernel;
    }

    public string SessionId => _session?.SessionId ?? string.Empty;

    public bool IsConnected => _session?.IsConnected ?? false;

    public IReadOnlyList<IPage> Pages => _session?.Pages ?? Array.Empty<IPage>();

    public async Task<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default)
    {
        IBrowserSession session = await GetSessionAsync(ct).ConfigureAwait(false);
        return await session.NewPageAsync(options, ct).ConfigureAwait(false);
    }

    public async Task<IPage?> GetPageAsync(string pageId, CancellationToken ct = default)
    {
        IBrowserSession session = await GetSessionAsync(ct).ConfigureAwait(false);
        return await session.GetPageAsync(pageId, ct).ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        IBrowserSession session = await GetSessionAsync(ct).ConfigureAwait(false);
        await session.CloseAsync(ct).ConfigureAwait(false);
    }

    public async Task SaveStorageStateAsync(string path)
    {
        IBrowserSession session = await GetSessionAsync(CancellationToken.None).ConfigureAwait(false);
        await session.SaveStorageStateAsync(path).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
        }

        _initializationGate.Dispose();
    }

    private async Task<Ghost.IBrowserSession> GetSessionAsync(CancellationToken ct)
    {
        if (_session is not null)
        {
            return _session;
        }

        if (_sessionTask is null)
        {
            await _initializationGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _sessionTask ??= _kernel.NewSessionAsync(ct: ct);
            }
            finally
            {
                _initializationGate.Release();
            }
        }

        _session = await _sessionTask.ConfigureAwait(false);
        return _session;
    }
}
