namespace Ghost.Testing.Fakes;

public class FakeElement : IElement
{
    private readonly Dictionary<string, string> _attributes = [];
    private string _textContent = string.Empty;
    private string _innerHTML = string.Empty;

    public string SelectorPath { get; } = "fake-selector";

    public Task<string?> GetAttributeAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(_attributes.TryGetValue(name, out string? value) ? value : null);

    public Task<string?> GetTextContentAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>(_textContent);

    public Task<string?> GetInnerHtmlAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>(_innerHTML);

    /// <summary>
    /// Helper method for tests to set element attributes.
    /// </summary>
    public void SetAttribute(string name, string value)
    {
        _attributes[name] = value;
    }

    /// <summary>
    /// Helper method for tests to set text content.
    /// </summary>
    public void SetTextContent(string content)
    {
        _textContent = content;
    }

    /// <summary>
    /// Helper method for tests to set inner HTML.
    /// </summary>
    public void SetInnerHtml(string html)
    {
        _innerHTML = html;
    }

    public Task<bool> IsVisibleAsync(CancellationToken ct = default) =>
        Task.FromResult(true);

    public Task<bool> IsEnabledAsync(CancellationToken ct = default) =>
        Task.FromResult(true);

    public Task<bool> IsCheckedAsync(CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task ClickAsync(ClickOptions? options = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task TypeAsync(string text, TypeOptions? options = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task FillAsync(string value, TypeOptions? options = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task FocusAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task HoverAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ScrollIntoViewAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default) =>
        Task.FromResult<IElement?>(new FakeElement());

    public Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<IElement>>([]);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
