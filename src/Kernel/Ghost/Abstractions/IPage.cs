using System.Text.Json;

namespace Ghost;

public interface IPage : IAsyncDisposable
{
    public string PageId { get; }
    public string Url { get; }
    public Task<string?> GetTitleAsync(CancellationToken ct = default);
    public Task NavigateAsync(string url, NavigationOptions? options = null, CancellationToken ct = default);
    public Task GoBackAsync(NavigationOptions? options = null, CancellationToken ct = default);
    public Task GoForwardAsync(NavigationOptions? options = null, CancellationToken ct = default);
    public Task ReloadAsync(NavigationOptions? options = null, CancellationToken ct = default);

    public Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default);
    public Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);

    public Task ClickAsync(string selector, ClickOptions? options = null, CancellationToken ct = default);
    public Task TypeAsync(string selector, string text, TypeOptions? options = null, CancellationToken ct = default);
    public Task FillAsync(string selector, string value, TypeOptions? options = null, CancellationToken ct = default);
    public Task SelectOptionAsync(string selector, IEnumerable<string> values, CancellationToken ct = default);
    public Task CheckAsync(string selector, CancellationToken ct = default);
    public Task UncheckAsync(string selector, CancellationToken ct = default);

    public Task<IElement> WaitForSelectorAsync(string selector, WaitOptions? options = null, CancellationToken ct = default);
    public Task WaitForNavigationAsync(NavigationOptions? options = null, CancellationToken ct = default);
    public Task WaitForLoadStateAsync(WaitOptions? options = null, CancellationToken ct = default);

    public Task<T> EvaluateAsync<T>(string script, object? arg = null, CancellationToken ct = default);
    public Task<object?> EvaluateHandleAsync(string script, object? arg = null, CancellationToken ct = default);

    public Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default);
    public Task<byte[]> PdfAsync(CancellationToken ct = default);

    public Task<string> GetContentAsync(CancellationToken ct = default);
    public Task SetContentAsync(string html, CancellationToken ct = default);

    public Task FocusAsync(string selector, CancellationToken ct = default);
    public Task HoverAsync(string selector, CancellationToken ct = default);
    public Task PressAsync(string selector, string key, CancellationToken ct = default);

    /// <summary>
    /// Adds cookies to the browser context.
    /// </summary>
    /// <param name="cookies">The cookies to add.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task AddCookiesAsync(IEnumerable<Ghost.Cookie> cookies, CancellationToken ct = default);
}
