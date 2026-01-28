using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Patchright;

namespace Ghost.Internal;

internal sealed class PageWrapper : IPage
{
    private readonly Patchright.IPage _page;
    private bool _disposed;

    public PageWrapper(Patchright.IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = page;
        PageId = Guid.NewGuid().ToString();
    }

    public string PageId { get; }

    public string Url => _page.Url;

    public string? Title => _page.Title;

    public async Task NavigateAsync(string url, NavigationOptions? options = null, CancellationToken ct = default)
    {
        var nav = options ?? new NavigationOptions();
        await _page.GoToAsync(url, new Patchright.NavigationOptions { Timeout = nav.Timeout, WaitUntil = Map(nav.WaitUntil) }, ct);
    }

    private static Patchright.WaitUntil Map(WaitUntil w) => w switch
    {
        WaitUntil.DomContentLoaded => Patchright.WaitUntil.DomContentLoaded,
        WaitUntil.NetworkIdle => Patchright.WaitUntil.NetworkIdle,
        _ => Patchright.WaitUntil.Load
    };

    public Task GoBackAsync(NavigationOptions? options = null, CancellationToken ct = default) => _page.GoBackAsync(ct);
    public Task GoForwardAsync(NavigationOptions? options = null, CancellationToken ct = default) => _page.GoForwardAsync(ct);
    public Task ReloadAsync(NavigationOptions? options = null, CancellationToken ct = default) => _page.ReloadAsync(ct);

    public async Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default)
    {
        var handle = await _page.QuerySelectorAsync(selector, ct);
        return handle is null ? null : new ElementWrapper(handle);
    }

    public async Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default)
    {
        var handles = await _page.QuerySelectorAllAsync(selector, ct);
        return handles.Select(h => (IElement)new ElementWrapper(h)).ToList();
    }

    public Task ClickAsync(string selector, ClickOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new ClickOptions();
        return _page.ClickAsync(selector, new Patchright.ClickOptions { Button = o.Button, ClickCount = o.ClickCount, Delay = o.Delay, Modifiers = o.Modifiers.ToArray() }, ct);
    }

    public Task TypeAsync(string selector, string text, TypeOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new TypeOptions();
        return _page.TypeAsync(selector, text, new Patchright.TypeOptions { Delay = o.Delay }, ct);
    }

    public Task FillAsync(string selector, string value, TypeOptions? options = null, CancellationToken ct = default)
        => _page.FillAsync(selector, value, ct);

    public Task SelectOptionAsync(string selector, IEnumerable<string> values, CancellationToken ct = default)
        => _page.SelectOptionAsync(selector, values.ToArray(), ct);

    public Task CheckAsync(string selector, CancellationToken ct = default) => _page.CheckAsync(selector, ct);
    public Task UncheckAsync(string selector, CancellationToken ct = default) => _page.UncheckAsync(selector, ct);

    public async Task<IElement> WaitForSelectorAsync(string selector, WaitOptions? options = null, CancellationToken ct = default)
    {
        var handle = await _page.WaitForSelectorAsync(selector, new Patchright.WaitForSelectorOptions { Timeout = options?.Timeout ?? 30_000, State = MapState(options?.State ?? WaitState.Load) }, ct);
        return new ElementWrapper(handle!);
    }

    private static Patchright.WaitState MapState(WaitState s) => s switch
    {
        WaitState.Attached => Patchright.WaitState.Attached,
        WaitState.Detached => Patchright.WaitState.Detached,
        WaitState.Visible => Patchright.WaitState.Visible,
        WaitState.Hidden => Patchright.WaitState.Hidden,
        _ => Patchright.WaitState.Load
    };

    public Task WaitForNavigationAsync(NavigationOptions? options = null, CancellationToken ct = default)
        => _page.WaitForNavigationAsync(new Patchright.NavigationOptions { Timeout = options?.Timeout ?? 30_000, WaitUntil = Map(options?.WaitUntil ?? WaitUntil.Load) }, ct);

    public Task WaitForLoadStateAsync(WaitOptions? options = null, CancellationToken ct = default)
        => _page.WaitForLoadStateAsync(MapState(options?.State ?? WaitState.Load), options?.Timeout ?? 30_000, ct);

    public async Task<T> EvaluateAsync<T>(string script, object? arg = null, CancellationToken ct = default)
    {
        var res = await _page.EvaluateAsync<T>(script, arg, ct);
        return res;
    }

    public Task<object?> EvaluateHandleAsync(string script, object? arg = null, CancellationToken ct = default)
        => _page.EvaluateHandleAsync<object?>(script, arg, ct);

    public Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new ScreenshotOptions();
        return _page.ScreenshotAsync(new Patchright.ScreenshotOptions { Type = o.Type, Quality = o.Quality, FullPage = o.FullPage }, ct);
    }

    public Task<byte[]> PdfAsync(CancellationToken ct = default) => _page.PdfAsync(ct);

    public Task<string> GetContentAsync(CancellationToken ct = default) => _page.GetContentAsync(ct);
    public Task SetContentAsync(string html, CancellationToken ct = default) => _page.SetContentAsync(html, ct);

    public Task FocusAsync(string selector, CancellationToken ct = default) => _page.FocusAsync(selector, ct);
    public Task HoverAsync(string selector, CancellationToken ct = default) => _page.HoverAsync(selector, ct);
    public Task PressAsync(string selector, string key, CancellationToken ct = default) => _page.PressAsync(selector, key, ct);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _page.DisposeAsync();
        _disposed = true;
    }
}
