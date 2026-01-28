using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Ghost.Internal;

internal sealed class PageWrapper : IPage
{
    private readonly Microsoft.Playwright.IPage _page;
    private bool _disposed;

    public PageWrapper(Microsoft.Playwright.IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = page;
        PageId = Guid.NewGuid().ToString();
    }

    public string PageId { get; }

    public string Url => _page.Url;

    public string? Title => _page.TitleAsync().GetAwaiter().GetResult();

    public async Task NavigateAsync(string url, NavigationOptions? options = null, CancellationToken ct = default)
    {
        var nav = options ?? new NavigationOptions();
        await _page.GotoAsync(url, new Microsoft.Playwright.PageGotoOptions { Timeout = nav.Timeout, WaitUntil = Map(nav.WaitUntil) });
    }

    private static Microsoft.Playwright.WaitUntilState Map(WaitUntil w) => w switch
    {
        WaitUntil.DomContentLoaded => Microsoft.Playwright.WaitUntilState.DOMContentLoaded,
        WaitUntil.NetworkIdle => Microsoft.Playwright.WaitUntilState.NetworkIdle,
        _ => Microsoft.Playwright.WaitUntilState.Load
    };

    public Task GoBackAsync(NavigationOptions? options = null, CancellationToken ct = default) => _page.GoBackAsync();
    public Task GoForwardAsync(NavigationOptions? options = null, CancellationToken ct = default) => _page.GoForwardAsync();
    public Task ReloadAsync(NavigationOptions? options = null, CancellationToken ct = default) => _page.ReloadAsync();

    public async Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default)
    {
        var handle = await _page.QuerySelectorAsync(selector);
        return handle is null ? null : new ElementWrapper(handle);
    }

    public async Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default)
    {
        var handles = await _page.QuerySelectorAllAsync(selector);
        return handles.Select(h => (IElement)new ElementWrapper(h)).ToList();
    }

    public Task ClickAsync(string selector, ClickOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new ClickOptions();
        return _page.ClickAsync(selector, new Microsoft.Playwright.PageClickOptions { Button = ParseButton(o.Button), ClickCount = o.ClickCount, Delay = o.Delay, Modifiers = o.Modifiers.Select(ParseModifier).ToArray() });
    }

    public Task TypeAsync(string selector, string text, TypeOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new TypeOptions();
        // Use dynamic to avoid calling obsolete API overloads directly
        return ((dynamic)_page).TypeAsync(selector, text, new Microsoft.Playwright.PageTypeOptions { Delay = o.Delay });
    }

    public Task FillAsync(string selector, string value, TypeOptions? options = null, CancellationToken ct = default)
        => _page.FillAsync(selector, value);

    public Task SelectOptionAsync(string selector, IEnumerable<string> values, CancellationToken ct = default)
        => _page.SelectOptionAsync(selector, values.ToArray());

    public Task CheckAsync(string selector, CancellationToken ct = default) => _page.CheckAsync(selector);
    public Task UncheckAsync(string selector, CancellationToken ct = default) => _page.UncheckAsync(selector);

    public async Task<IElement> WaitForSelectorAsync(string selector, WaitOptions? options = null, CancellationToken ct = default)
    {
        var handle = await _page.WaitForSelectorAsync(selector, new Microsoft.Playwright.PageWaitForSelectorOptions { Timeout = options?.Timeout ?? 30_000, State = MapState(options?.State ?? WaitState.Load) });
        return new ElementWrapper(handle!);
    }

    private static Microsoft.Playwright.WaitForSelectorState MapState(WaitState s) => s switch
    {
        WaitState.Attached => Microsoft.Playwright.WaitForSelectorState.Attached,
        WaitState.Detached => Microsoft.Playwright.WaitForSelectorState.Detached,
        WaitState.Visible => Microsoft.Playwright.WaitForSelectorState.Visible,
        WaitState.Hidden => Microsoft.Playwright.WaitForSelectorState.Hidden,
        _ => Microsoft.Playwright.WaitForSelectorState.Visible
    };

    public Task WaitForNavigationAsync(NavigationOptions? options = null, CancellationToken ct = default)
        => ((dynamic)_page).WaitForNavigationAsync(new Microsoft.Playwright.PageWaitForNavigationOptions { Timeout = options?.Timeout ?? 30_000, WaitUntil = Map(options?.WaitUntil ?? WaitUntil.Load) });

    public Task WaitForLoadStateAsync(WaitOptions? options = null, CancellationToken ct = default)
        => _page.WaitForLoadStateAsync(MapLoadState(options?.State ?? WaitState.Load));

    private static Microsoft.Playwright.LoadState MapLoadState(WaitState s) => s switch
    {
        WaitState.Load => Microsoft.Playwright.LoadState.Load,
        WaitState.Attached => Microsoft.Playwright.LoadState.DOMContentLoaded,
        WaitState.Detached => Microsoft.Playwright.LoadState.DOMContentLoaded,
        WaitState.Visible => Microsoft.Playwright.LoadState.DOMContentLoaded,
        WaitState.Hidden => Microsoft.Playwright.LoadState.DOMContentLoaded,
        _ => Microsoft.Playwright.LoadState.Load
    };

    public async Task<T> EvaluateAsync<T>(string script, object? arg = null, CancellationToken ct = default)
    {
        var res = await _page.EvaluateAsync<T>(script, arg);
        return res;
    }

    public async Task<object?> EvaluateHandleAsync(string script, object? arg = null, CancellationToken ct = default)
    {
        var handle = await _page.EvaluateHandleAsync(script, arg);
        return handle;
    }

    public Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new ScreenshotOptions();
        return _page.ScreenshotAsync(new Microsoft.Playwright.PageScreenshotOptions { Type = ParseScreenshotType(o.Type), Quality = o.Quality, FullPage = o.FullPage });
    }

    public Task<byte[]> PdfAsync(CancellationToken ct = default) => _page.PdfAsync();

    public Task<string> GetContentAsync(CancellationToken ct = default) => _page.ContentAsync();
    public Task SetContentAsync(string html, CancellationToken ct = default) => _page.SetContentAsync(html);

    public Task FocusAsync(string selector, CancellationToken ct = default) => _page.FocusAsync(selector);
    public Task HoverAsync(string selector, CancellationToken ct = default) => _page.HoverAsync(selector);
    public Task PressAsync(string selector, string key, CancellationToken ct = default) => _page.PressAsync(selector, key);

    private static Microsoft.Playwright.MouseButton? ParseButton(string? btn) => btn?.ToLowerInvariant() switch
    {
        "left" => Microsoft.Playwright.MouseButton.Left,
        "right" => Microsoft.Playwright.MouseButton.Right,
        "middle" => Microsoft.Playwright.MouseButton.Middle,
        _ => Microsoft.Playwright.MouseButton.Left
    };

    private static Microsoft.Playwright.KeyboardModifier ParseModifier(string m) => m.ToLowerInvariant() switch
    {
        "alt" => Microsoft.Playwright.KeyboardModifier.Alt,
        "control" => Microsoft.Playwright.KeyboardModifier.Control,
        "meta" => Microsoft.Playwright.KeyboardModifier.Meta,
        "shift" => Microsoft.Playwright.KeyboardModifier.Shift,
        _ => Microsoft.Playwright.KeyboardModifier.Alt
    };

    private static Microsoft.Playwright.ScreenshotType? ParseScreenshotType(string? t) => t?.ToLowerInvariant() switch
    {
        "png" => Microsoft.Playwright.ScreenshotType.Png,
        "jpeg" => Microsoft.Playwright.ScreenshotType.Jpeg,
        _ => Microsoft.Playwright.ScreenshotType.Png
    };

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try
        {
            await _page.CloseAsync();
        }
        catch (Exception)
        {
            // Ignore errors during disposal (e.g. browser already closed)
        }
        _disposed = true;
    }
}
