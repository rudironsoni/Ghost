using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Patchright;
using Ghost;

#pragma warning disable IDE0032
namespace Ghost.Internal;

internal sealed class BrowserSessionWrapper : IBrowserSession
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
        var page = await _context.NewPageAsync(ct);
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
        await _context.CloseAsync(ct);
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        foreach (var p in _pages.Cast<PageWrapper>())
        {
            await p.DisposeAsync();
        }
        await _context.DisposeAsync();
        _onDispose?.Invoke();
        _disposed = true;
    }
}
