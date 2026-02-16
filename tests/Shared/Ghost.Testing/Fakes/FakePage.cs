namespace Ghost.Testing.Fakes;

public class FakePage : IPage
{
    private string _content = "<html><body></body></html>";
    private readonly Dictionary<string, FakeElement> _elements = [];

    public string PageId { get; } = Guid.NewGuid().ToString();
    public string Url { get; private set; } = "about:blank";
    private string? _title;

    public Task<string?> GetTitleAsync(CancellationToken ct = default) => Task.FromResult(_title);

    public Task NavigateAsync(string url, NavigationOptions? options = null, CancellationToken ct = default)
    {
        Url = url;
        _title = $"Page: {url}";
        return Task.CompletedTask;
    }

    public Task GoBackAsync(NavigationOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task GoForwardAsync(NavigationOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task ReloadAsync(NavigationOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IElement?> QuerySelectorAsync(string selector, CancellationToken ct = default)
    {
        if (_elements.TryGetValue(selector, out FakeElement? element))
        {
            return Task.FromResult<IElement?>(element);
        }
        return Task.FromResult<IElement?>(new FakeElement());
    }

    public Task<IReadOnlyList<IElement>> QuerySelectorAllAsync(string selector, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<IElement>>([]);

    public Task ClickAsync(string selector, ClickOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task TypeAsync(string selector, string text, TypeOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task FillAsync(string selector, string value, TypeOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task SelectOptionAsync(string selector, IEnumerable<string> values, CancellationToken ct = default) => Task.CompletedTask;
    public Task CheckAsync(string selector, CancellationToken ct = default) => Task.CompletedTask;
    public Task UncheckAsync(string selector, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IElement> WaitForSelectorAsync(string selector, WaitOptions? options = null, CancellationToken ct = default) =>
        Task.FromResult<IElement>(new FakeElement());

    public Task WaitForNavigationAsync(NavigationOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task WaitForLoadStateAsync(WaitOptions? options = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task<T> EvaluateAsync<T>(string script, object? arg = null, CancellationToken ct = default) =>
        Task.FromResult<T>(default!);

    public Task<object?> EvaluateHandleAsync(string script, object? arg = null, CancellationToken ct = default) =>
        Task.FromResult<object?>(null);

    public Task<byte[]> ScreenshotAsync(ScreenshotOptions? options = null, CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task<byte[]> PdfAsync(CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task<string> GetContentAsync(CancellationToken ct = default) =>
        Task.FromResult(_content);

    public Task SetContentAsync(string html, CancellationToken ct = default)
    {
        _content = html;
        return Task.CompletedTask;
    }

    public Task FocusAsync(string selector, CancellationToken ct = default) => Task.CompletedTask;
    public Task HoverAsync(string selector, CancellationToken ct = default) => Task.CompletedTask;
    public Task PressAsync(string selector, string key, CancellationToken ct = default) => Task.CompletedTask;

    public Task AddCookiesAsync(IEnumerable<Ghost.Cookie> cookies, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Helper method for tests to register elements that can be queried.
    /// </summary>
    public void RegisterElement(string selector, FakeElement element)
    {
        _elements[selector] = element;
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
