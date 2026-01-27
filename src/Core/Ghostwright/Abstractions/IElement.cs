namespace Ghostwright;

public interface IElement : IAsyncDisposable
{
    string SelectorPath { get; }
    Task ClickAsync(ClickOptions? options = null, CancellationToken ct = default);
    Task TypeAsync(string text, TypeOptions? options = null, CancellationToken ct = default);
    Task FillAsync(string value, TypeOptions? options = null, CancellationToken ct = default);

    Task<string?> GetAttributeAsync(string name, CancellationToken ct = default);
    Task<string?> GetTextContentAsync(CancellationToken ct = default);
    Task<string?> GetInnerHtmlAsync(CancellationToken ct = default);

    Task<bool> IsVisibleAsync(CancellationToken ct = default);
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task<bool> IsCheckedAsync(CancellationToken ct = default);

    Task HoverAsync(CancellationToken ct = default);
    Task FocusAsync(CancellationToken ct = default);
    Task ScrollIntoViewAsync(CancellationToken ct = default);

    Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default);

    Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default);
    Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);
}
