using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Ghost;

#pragma warning disable IDE0032
namespace Ghost.Internal;

internal sealed class BrowserSessionWrapper : IBrowserSession, IDisposable
{
    private readonly IBrowserContext _context;
    private List<IPage> _pages { get; } = new List<IPage>();
    private readonly string _sessionId;
    private readonly Action? _onDispose;
    private bool _disposed;

    public BrowserSessionWrapper(IBrowserContext context, string sessionId, Action? onDispose = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _sessionId = sessionId ?? Guid.NewGuid().ToString();
        _onDispose = onDispose;
    }

    public string SessionId => _sessionId;
    public bool IsConnected => !_disposed;
    public IReadOnlyList<IPage> Pages => _pages.AsReadOnly();

    public async ValueTask<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default)
    {
        var page = await _context.NewPageAsync();
        var wrapper = new PageWrapper(page);
        _pages.Add(wrapper);
        return wrapper;
    }

    public ValueTask<IPage?> GetPageAsync(string pageId, CancellationToken ct = default)
    {
        var page = _pages.OfType<PageWrapper>().FirstOrDefault(p => p.PageId == pageId);
        return new ValueTask<IPage?>(page);
    }

    public async ValueTask CloseAsync(CancellationToken ct = default)
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
}
