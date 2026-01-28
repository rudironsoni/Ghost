using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Patchright;

namespace Ghost.Internal;

internal sealed class ElementWrapper : IElement
{
    private readonly Patchright.IElementHandle _handle;
    private bool _disposed;

    public ElementWrapper(Patchright.IElementHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        _handle = handle;
        SelectorPath = handle.ToString() ?? Guid.NewGuid().ToString();
    }

    public string SelectorPath { get; }

    public Task ClickAsync(ClickOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new ClickOptions();
        return _handle.ClickAsync(new Patchright.ClickOptions { Button = o.Button, ClickCount = o.ClickCount, Delay = o.Delay, Modifiers = o.Modifiers.ToArray() }, ct);
    }

    public Task TypeAsync(string text, TypeOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new TypeOptions();
        return _handle.TypeAsync(text, new Patchright.TypeOptions { Delay = o.Delay }, ct);
    }

    public Task FillAsync(string value, TypeOptions? options = null, CancellationToken ct = default) => _handle.FillAsync(value, ct);

    public Task<string?> GetAttributeAsync(string name, CancellationToken ct = default) => _handle.GetAttributeAsync(name, ct);
    public Task<string?> GetTextContentAsync(CancellationToken ct = default) => _handle.TextContentAsync(ct);
    public Task<string?> GetInnerHtmlAsync(CancellationToken ct = default) => _handle.InnerHtmlAsync(ct);

    public Task<bool> IsVisibleAsync(CancellationToken ct = default) => _handle.IsVisibleAsync(ct);
    public Task<bool> IsEnabledAsync(CancellationToken ct = default) => _handle.IsEnabledAsync(ct);
    public Task<bool> IsCheckedAsync(CancellationToken ct = default) => _handle.IsCheckedAsync(ct);

    public Task HoverAsync(CancellationToken ct = default) => _handle.HoverAsync(ct);
    public Task FocusAsync(CancellationToken ct = default) => _handle.FocusAsync(ct);
    public Task ScrollIntoViewAsync(CancellationToken ct = default) => _handle.ScrollIntoViewAsync(ct);

    public Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new ScreenshotOptions();
        return _handle.ScreenshotAsync(new Patchright.ScreenshotOptions { Type = o.Type, Quality = o.Quality, FullPage = o.FullPage }, ct);
    }

    public async Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default)
    {
        var child = await _handle.QuerySelectorAsync(selector, ct);
        return child is null ? null : new ElementWrapper(child);
    }

    public async Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default)
    {
        var handles = await _handle.QuerySelectorAllAsync(selector, ct);
        return handles.Select(h => (IElement)new ElementWrapper(h)).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _handle.DisposeAsync();
        _disposed = true;
    }
}
