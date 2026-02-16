using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost;
using Microsoft.Playwright;

namespace Ghost.Internal;

internal sealed class BrowserSessionWrapper : IBrowserSession, IDisposable
{
    private readonly IBrowserContext _context;
    private readonly List<IPage> _pages = [];
    private readonly IAsyncDisposable? _bridge;
    // SessionId property replaces backing field to satisfy IDE0032
    public string SessionId { get; }
    private readonly Action? _onDispose;
    private bool _disposed;

    public BrowserSessionWrapper(IBrowserContext context, string sessionId, Action? onDispose = null, IAsyncDisposable? bridge = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        SessionId = sessionId ?? Guid.NewGuid().ToString();
        _onDispose = onDispose;
        _bridge = bridge;
    }
    public bool IsConnected => !_disposed;
    public IReadOnlyList<IPage> Pages => _pages.AsReadOnly();

    public async Task<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default)
    {
        Microsoft.Playwright.IPage page = await _context.NewPageAsync().ConfigureAwait(false);

        // Apply PageOptions overrides via InitScripts if provided
        if (options is not null)
        {
            if (!string.IsNullOrEmpty(options.TimezoneId))
            {
                await page.AddInitScriptAsync(Stealth.StealthScripts.GetTimezoneOverrideScript(options.TimezoneId)).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(options.Locale))
            {
                await page.AddInitScriptAsync(Stealth.StealthScripts.GetLocaleOverrideScript(options.Locale)).ConfigureAwait(false);
            }
        }

        var wrapper = new PageWrapper(page);
        _pages.Add(wrapper);
        return wrapper;
    }

    public Task<IPage?> GetPageAsync(string pageId, CancellationToken ct = default)
    {
        PageWrapper? page = _pages.OfType<PageWrapper>().FirstOrDefault(p => p.PageId == pageId);
        return Task.FromResult<IPage?>(page);
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        if (_disposed) return;
        await _context.CloseAsync().ConfigureAwait(false);
        _disposed = true;
    }

#pragma warning disable IDE1006 // Naming rule violation: DisposeAsyncCore follows IAsyncDisposable pattern
    private async ValueTask DisposeAsyncCore()
    {
        // perform async cleanup
        foreach (PageWrapper p in _pages.Cast<PageWrapper>())
        {
            await p.DisposeAsync().ConfigureAwait(false);
        }
        if (_bridge is not null)
        {
            await _bridge.DisposeAsync().ConfigureAwait(false);
        }
        await _context.DisposeAsync().ConfigureAwait(false);
        _onDispose?.Invoke();
    }

#pragma warning restore IDE1006
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisposeAsyncCore().ConfigureAwait(false);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        // Note: We cannot call async cleanup synchronously here.
        // Callers should use DisposeAsync for proper cleanup.
        // If synchronous disposal is required, resources will be cleaned up by finalizer or process exit.
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async Task SaveStorageStateAsync(string path)
    {
        await _context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = path }).ConfigureAwait(false);
    }
}
