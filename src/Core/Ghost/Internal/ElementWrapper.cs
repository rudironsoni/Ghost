using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Ghost.Internal;

internal sealed class ElementWrapper : IElement, Ghost.IElementHandle
{
    private readonly Microsoft.Playwright.IElementHandle _handle;
    private bool _disposed;

    public ElementWrapper(Microsoft.Playwright.IElementHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        _handle = handle;
        SelectorPath = handle.ToString() ?? Guid.NewGuid().ToString();
    }

    public string SelectorPath { get; }

    public Task ClickAsync(ClickOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new ClickOptions();
        return _handle.ClickAsync(new Microsoft.Playwright.ElementHandleClickOptions { Button = ParseButton(o.Button), ClickCount = o.ClickCount, Delay = o.Delay, Modifiers = o.Modifiers.Select(ParseModifier).ToArray() });
    }

    public Task TypeAsync(string text, TypeOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new TypeOptions();
        // ElementHandle.TypeAsync is deprecated in Playwright; use PressSequentiallyAsync when available
        // Microsoft.Playwright 1.49.0 provides ElementHandle.PressSequentiallyAsync which supports delay
        return _handle.PressSequentiallyAsync(text, new Microsoft.Playwright.ElementHandlePressSequentiallyOptions { Delay = o.Delay });
    }

    public Task FillAsync(string value, TypeOptions? options = null, CancellationToken ct = default) => _handle.FillAsync(value);

    public Task<string?> GetAttributeAsync(string name, CancellationToken ct = default) => _handle.GetAttributeAsync(name);
    public async Task<string?> GetTextContentAsync(CancellationToken ct = default)
    {
        var res = await _handle.TextContentAsync();
        return res;
    }

    public async Task<string?> GetInnerHtmlAsync(CancellationToken ct = default)
    {
        var res = await _handle.InnerHTMLAsync();
        return res;
    }

    public Task<bool> IsVisibleAsync(CancellationToken ct = default) => _handle.IsVisibleAsync();
    public Task<bool> IsEnabledAsync(CancellationToken ct = default) => _handle.IsEnabledAsync();
    public Task<bool> IsCheckedAsync(CancellationToken ct = default) => _handle.IsCheckedAsync();

    public Task HoverAsync(CancellationToken ct = default) => _handle.HoverAsync();
    public Task FocusAsync(CancellationToken ct = default) => _handle.FocusAsync();
    public Task ScrollIntoViewAsync(CancellationToken ct = default) => _handle.ScrollIntoViewIfNeededAsync();

    public Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new ScreenshotOptions();
        return _handle.ScreenshotAsync(new Microsoft.Playwright.ElementHandleScreenshotOptions { Type = ParseScreenshotType(o.Type), Quality = o.Quality });
    }

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

    public async Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default)
    {
        var child = await _handle.QuerySelectorAsync(selector);
        return child is null ? null : new ElementWrapper(child);
    }

    public async Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default)
    {
        var handles = await _handle.QuerySelectorAllAsync(selector);
        return handles.Select(h => (IElement)new ElementWrapper(h)).ToList();
    }

    // Explicit IElementHandle implementation to avoid conflicting signatures with IElement methods
    async Task<Ghost.IElementHandle?> Ghost.IElementHandle.QuerySelectorAsync(string selector, CancellationToken ct)
    {
        var child = await _handle.QuerySelectorAsync(selector);
        return child is null ? null : new ElementWrapper(child);
    }

    async Task<IReadOnlyList<Ghost.IElementHandle>> Ghost.IElementHandle.QuerySelectorAllAsync(string selector, CancellationToken ct)
    {
        var handles = await _handle.QuerySelectorAllAsync(selector);
        return handles.Select(h => (Ghost.IElementHandle)new ElementWrapper(h)).ToList();
    }

    Task<string?> Ghost.IElementHandle.TextContentAsync(CancellationToken ct)
        => _handle.TextContentAsync();

    

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _handle.DisposeAsync();
        _disposed = true;
    }
}
