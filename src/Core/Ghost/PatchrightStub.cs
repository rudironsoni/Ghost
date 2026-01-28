using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Patchright;

public enum WaitUntil { Load, DomContentLoaded, NetworkIdle }
public enum WaitState { Attached, Detached, Visible, Hidden, Load }

public sealed class LaunchOptions
{
    public bool Headless { get; set; }
    public int SlowMo { get; set; }
    public Proxy? Proxy { get; set; }
}

public sealed class Proxy { public string? Server { get; set; } public string? Username { get; set; } public string? Password { get; set; } public string? Bypass { get; set; } }

public sealed class PlaywrightGeolocation { public double Latitude { get; set; } public double Longitude { get; set; } public double Accuracy { get; set; } }

public sealed class BrowserNewContextOptions
{
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }
    public string? UserAgent { get; set; }
    public Proxy? Proxy { get; set; }
    public PlaywrightGeolocation? Geolocation { get; set; }
    public List<string>? Permissions { get; set; }
}

public sealed class ClickOptions { public string? Button { get; set; } public int ClickCount { get; set; } public int Delay { get; set; } public string[] Modifiers { get; set; } = Array.Empty<string>(); }
public sealed class TypeOptions { public int Delay { get; set; } }
public sealed class WaitForSelectorOptions { public int Timeout { get; set; } public WaitState State { get; set; } }
public sealed class ScreenshotOptions { public string? Type { get; set; } public int? Quality { get; set; } public bool FullPage { get; set; } }
public sealed class NavigationOptions { public int Timeout { get; set; } public WaitUntil WaitUntil { get; set; } }

public interface IBrowser
{
    Task<IBrowserContext> NewContextAsync(BrowserNewContextOptions? options = null, CancellationToken ct = default);
    ValueTask DisposeAsync();
}

public interface IBrowserContext : IAsyncDisposable
{
    Task<IPage> NewPageAsync(CancellationToken ct = default);
    Task CloseAsync(CancellationToken ct = default);
}

public interface IPage : IAsyncDisposable
{
    string Url { get; }
    string? Title { get; }
    Task GoToAsync(string url, NavigationOptions? options = null, CancellationToken ct = default);
    Task GoBackAsync(CancellationToken ct = default);
    Task GoForwardAsync(CancellationToken ct = default);
    Task ReloadAsync(CancellationToken ct = default);

    Task<IElementHandle?> QuerySelectorAsync(string selector, CancellationToken ct = default);
    Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);

    Task ClickAsync(string selector, ClickOptions? options = null, CancellationToken ct = default);
    Task TypeAsync(string selector, string text, TypeOptions? options = null, CancellationToken ct = default);
    Task FillAsync(string selector, string value, CancellationToken ct = default);
    Task SelectOptionAsync(string selector, string[] values, CancellationToken ct = default);
    Task CheckAsync(string selector, CancellationToken ct = default);
    Task UncheckAsync(string selector, CancellationToken ct = default);

    Task<IElementHandle> WaitForSelectorAsync(string selector, WaitForSelectorOptions? options = null, CancellationToken ct = default);
    Task WaitForNavigationAsync(NavigationOptions? options = null, CancellationToken ct = default);
    Task WaitForLoadStateAsync(WaitState state, int timeout = 30_000, CancellationToken ct = default);

    Task<T> EvaluateAsync<T>(string script, object? arg = null, CancellationToken ct = default);
    Task<T?> EvaluateHandleAsync<T>(string script, object? arg = null, CancellationToken ct = default);

    Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default);
    Task<byte[]> PdfAsync(CancellationToken ct = default);

    Task<string> GetContentAsync(CancellationToken ct = default);
    Task SetContentAsync(string html, CancellationToken ct = default);

    Task FocusAsync(string selector, CancellationToken ct = default);
    Task HoverAsync(string selector, CancellationToken ct = default);
    Task PressAsync(string selector, string key, CancellationToken ct = default);
}

public interface IElementHandle : IAsyncDisposable
{
    Task ClickAsync(ClickOptions? options = null, CancellationToken ct = default);
    Task TypeAsync(string text, TypeOptions? options = null, CancellationToken ct = default);
    Task FillAsync(string value, CancellationToken ct = default);

    Task<string?> GetAttributeAsync(string name, CancellationToken ct = default);
    Task<string?> TextContentAsync(CancellationToken ct = default);
    Task<string?> InnerHtmlAsync(CancellationToken ct = default);

    Task<bool> IsVisibleAsync(CancellationToken ct = default);
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task<bool> IsCheckedAsync(CancellationToken ct = default);

    Task HoverAsync(CancellationToken ct = default);
    Task FocusAsync(CancellationToken ct = default);
    Task ScrollIntoViewAsync(CancellationToken ct = default);

    Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default);

    Task<IElementHandle?> QuerySelectorAsync(string selector, CancellationToken ct = default);
    Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);
}

public static class Patchright
{
    public static Task<IBrowser> LaunchAsync(LaunchOptions options, CancellationToken ct = default) => Task.FromException<IBrowser>(new NotImplementedException("Stubbed Patchright.LaunchAsync used only for compile-time tests"));
}
