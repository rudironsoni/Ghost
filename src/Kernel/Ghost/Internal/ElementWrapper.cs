using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        ClickOptions o = options ?? new ClickOptions();
        return _handle.ClickAsync(new Microsoft.Playwright.ElementHandleClickOptions { Button = ParseButton(o.Button), ClickCount = o.ClickCount, Delay = o.Delay, Modifiers = o.Modifiers.Select(ParseModifier).ToArray() });
    }

    public Task TypeAsync(string text, TypeOptions? options = null, CancellationToken ct = default)
    {
        TypeOptions o = options ?? new TypeOptions();
        // Prefer calling PressSequentiallyAsync if available on the runtime Playwright implementation.
        // Use reflection so this compiles against multiple Playwright versions without directly
        // referencing possibly-missing types. If not available, fall back to FillAsync (ignores delay).
        Type handleType = _handle.GetType();
        MethodInfo? method = handleType.GetMethod("PressSequentiallyAsync");
        if (method != null)
        {
            ParameterInfo[] parameters = method.GetParameters();
            try
            {
                if (parameters.Length == 2)
                {
                    Type optionsType = parameters[1].ParameterType;
                    object? optionsInstance = Activator.CreateInstance(optionsType);
                    PropertyInfo? delayProp = optionsType.GetProperty("Delay");
                    delayProp?.SetValue(optionsInstance, o.Delay);
                    object? result = method.Invoke(_handle, new object?[] { text, optionsInstance });
                    return (Task)result!;
                }

                if (parameters.Length == 1)
                {
                    object? result = method.Invoke(_handle, new object?[] { text });
                    return (Task)result!;
                }
            }
            catch
            {
                // If reflection invocation fails for any reason, fall back to FillAsync below
            }
        }

        // Fallback: Fill the element with the text (does not support per-character delay)
        return _handle.FillAsync(text);
    }

    public Task FillAsync(string value, TypeOptions? options = null, CancellationToken ct = default) => _handle.FillAsync(value);

    public Task<string?> GetAttributeAsync(string name, CancellationToken ct = default) => _handle.GetAttributeAsync(name);
    public async Task<string?> GetTextContentAsync(CancellationToken ct = default)
    {
        string? res = await _handle.TextContentAsync().ConfigureAwait(false);

        // Fallback to innerText if textContent is null or empty
        if (string.IsNullOrWhiteSpace(res))
        {
            try
            {
                res = await _handle.InnerTextAsync().ConfigureAwait(false);
            }
            catch { /* Ignore */ }
        }

        // Final fallback to JavaScript evaluation
        if (string.IsNullOrWhiteSpace(res))
        {
            try
            {
                res = await _handle.EvaluateAsync<string>("() => this.innerText || this.textContent || ''").ConfigureAwait(false);
            }
            catch { /* Ignore */ }
        }

        return res;
    }

    // Internal method to access the underlying Playwright handle for advanced scenarios
    internal Microsoft.Playwright.IElementHandle GetPlaywrightHandle() => _handle;

    public async Task<string?> GetInnerHtmlAsync(CancellationToken ct = default)
    {
        string res = await _handle.InnerHTMLAsync().ConfigureAwait(false);
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
        ScreenshotOptions o = options ?? new ScreenshotOptions();
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
        Microsoft.Playwright.IElementHandle? child = await _handle.QuerySelectorAsync(selector).ConfigureAwait(false);
        return child is null ? null : new ElementWrapper(child);
    }

    public async Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default)
    {
        IReadOnlyList<Microsoft.Playwright.IElementHandle> handles = await _handle.QuerySelectorAllAsync(selector).ConfigureAwait(false);
        return handles.Select(h => (IElement)new ElementWrapper(h)).ToList();
    }

    // Explicit IElementHandle implementation to avoid conflicting signatures with IElement methods
    async Task<Ghost.IElementHandle?> Ghost.IElementHandle.QuerySelectorAsync(string selector, CancellationToken ct)
    {
        Microsoft.Playwright.IElementHandle? child = await _handle.QuerySelectorAsync(selector).ConfigureAwait(false);
        return child is null ? null : new ElementWrapper(child);
    }

    async Task<IReadOnlyList<Ghost.IElementHandle>> Ghost.IElementHandle.QuerySelectorAllAsync(string selector, CancellationToken ct)
    {
        IReadOnlyList<Microsoft.Playwright.IElementHandle> handles = await _handle.QuerySelectorAllAsync(selector).ConfigureAwait(false);
        return handles.Select(h => (Ghost.IElementHandle)new ElementWrapper(h)).ToList();
    }

    Task<string?> Ghost.IElementHandle.TextContentAsync(CancellationToken ct)
        => _handle.TextContentAsync();

    public Task<T> EvaluateAsync<T>(string expression, CancellationToken ct = default)
        => _handle.EvaluateAsync<T>(expression);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _handle.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
