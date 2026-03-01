namespace Ghost;

public interface IElement : IAsyncDisposable
{
    public string SelectorPath { get; }
    public Task ClickAsync(ClickOptions? options = null, CancellationToken ct = default);
    public Task TypeAsync(string text, TypeOptions? options = null, CancellationToken ct = default);
    public Task FillAsync(string value, TypeOptions? options = null, CancellationToken ct = default);

    public Task<string?> GetAttributeAsync(string name, CancellationToken ct = default);
    public Task<string?> GetTextContentAsync(CancellationToken ct = default);
    public Task<string?> GetInnerHtmlAsync(CancellationToken ct = default);

    public Task<bool> IsVisibleAsync(CancellationToken ct = default);
    public Task<bool> IsEnabledAsync(CancellationToken ct = default);
    public Task<bool> IsCheckedAsync(CancellationToken ct = default);

    public Task HoverAsync(CancellationToken ct = default);
    public Task FocusAsync(CancellationToken ct = default);
    public Task ScrollIntoViewAsync(CancellationToken ct = default);

    public Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default);

    public Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default);
    public Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);
}
