using System.Text.Json;

namespace Ghost;

public interface IPage : IAsyncDisposable
{
    string PageId { get; }
    string Url { get; }
    string? Title { get; }
    Task NavigateAsync(string url, NavigationOptions? options = null, CancellationToken ct = default);
    Task GoBackAsync(NavigationOptions? options = null, CancellationToken ct = default);
    Task GoForwardAsync(NavigationOptions? options = null, CancellationToken ct = default);
    Task ReloadAsync(NavigationOptions? options = null, CancellationToken ct = default);

    Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default);
    Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);

    Task ClickAsync(string selector, ClickOptions? options = null, CancellationToken ct = default);
    Task TypeAsync(string selector, string text, TypeOptions? options = null, CancellationToken ct = default);
    Task FillAsync(string selector, string value, TypeOptions? options = null, CancellationToken ct = default);
    Task SelectOptionAsync(string selector, IEnumerable<string> values, CancellationToken ct = default);
    Task CheckAsync(string selector, CancellationToken ct = default);
    Task UncheckAsync(string selector, CancellationToken ct = default);

    Task<IElement> WaitForSelectorAsync(string selector, WaitOptions? options = null, CancellationToken ct = default);
    Task WaitForNavigationAsync(NavigationOptions? options = null, CancellationToken ct = default);
    Task WaitForLoadStateAsync(WaitOptions? options = null, CancellationToken ct = default);

    Task<T> EvaluateAsync<T>(string script, object? arg = null, CancellationToken ct = default);
    Task<object?> EvaluateHandleAsync(string script, object? arg = null, CancellationToken ct = default);

    Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default);
    Task<byte[]> PdfAsync(CancellationToken ct = default);

    Task<string> GetContentAsync(CancellationToken ct = default);
    Task SetContentAsync(string html, CancellationToken ct = default);

    Task FocusAsync(string selector, CancellationToken ct = default);
    Task HoverAsync(string selector, CancellationToken ct = default);
    Task PressAsync(string selector, string key, CancellationToken ct = default);
}
