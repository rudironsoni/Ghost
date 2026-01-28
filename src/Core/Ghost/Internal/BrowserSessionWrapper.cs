using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Ghost;

namespace Ghost.Internal;

internal sealed class BrowserSessionWrapper : IBrowserSession, IDisposable
{
    private readonly IBrowserContext _context;
    private readonly List<IPage> _pages = new();
    // SessionId property replaces backing field to satisfy IDE0032
    public string SessionId { get; }
    private readonly Action? _onDispose;
    private bool _disposed;

    public BrowserSessionWrapper(IBrowserContext context, string sessionId, Action? onDispose = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        SessionId = sessionId ?? Guid.NewGuid().ToString();
        _onDispose = onDispose;
    }
    public bool IsConnected => !_disposed;
    public IReadOnlyList<IPage> Pages => _pages.AsReadOnly();

    public async Task<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default)
    {
        var page = await _context.NewPageAsync();
        
        // Apply PageOptions overrides via InitScripts if provided
        if (options is not null)
        {
            if (!string.IsNullOrEmpty(options.TimezoneId))
            {
                await page.AddInitScriptAsync(Ghost.Stealth.StealthScripts.GetTimezoneOverrideScript(options.TimezoneId));
            }

            if (!string.IsNullOrEmpty(options.Locale))
            {
                await page.AddInitScriptAsync(Ghost.Stealth.StealthScripts.GetLocaleOverrideScript(options.Locale));
            }
        }

        var wrapper = new PageWrapper(page);
        _pages.Add(wrapper);
        return wrapper;
    }

    public Task<IPage?> GetPageAsync(string pageId, CancellationToken ct = default)
    {
        var page = _pages.OfType<PageWrapper>().FirstOrDefault(p => p.PageId == pageId);
        return Task.FromResult<IPage?>(page);
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        if (_disposed) return;
        await _context.CloseAsync();
        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        // perform async cleanup
        foreach (var p in _pages.Cast<PageWrapper>())
        {
            await p.DisposeAsync();
        }
        await _context.DisposeAsync();
        _onDispose?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisposeAsyncCore();
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // Block synchronously to run async cleanup
            DisposeAsyncCore().AsTask().GetAwaiter().GetResult();
        }
        _disposed = true;
    }

    public async Task SaveStorageStateAsync(string path)
    {
        await _context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = path });
    }
}
