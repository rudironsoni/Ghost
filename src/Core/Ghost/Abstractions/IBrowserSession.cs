namespace Ghost;

public interface IBrowserSession : IAsyncDisposable
{
    public string SessionId { get; }
    public bool IsConnected { get; }
    public IReadOnlyList<IPage> Pages { get; }
    public Task<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default);
    public Task<IPage?> GetPageAsync(string pageId, CancellationToken ct = default);
    public Task CloseAsync(CancellationToken ct = default);
    public Task SaveStorageStateAsync(string path);
}
