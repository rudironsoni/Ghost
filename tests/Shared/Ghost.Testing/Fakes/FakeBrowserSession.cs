namespace Ghost.Testing.Fakes;

public class FakeBrowserSession : IBrowserSession
{
    private readonly List<IPage> _pages = [];

    public string SessionId { get; } = Guid.NewGuid().ToString();
    public bool IsConnected { get; } = true;
    public IReadOnlyList<IPage> Pages => _pages;

    public Task<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default)
    {
        var page = new FakePage();
        _pages.Add(page);
        return Task.FromResult<IPage>(page);
    }

    public Task<IPage?> GetPageAsync(string pageId, CancellationToken ct = default) =>
        Task.FromResult(_pages.FirstOrDefault(p => p.PageId == pageId));

    public Task CloseAsync(CancellationToken ct = default)
    {
        _pages.Clear();
        return Task.CompletedTask;
    }

    public Task SaveStorageStateAsync(string path) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _pages.Clear();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
