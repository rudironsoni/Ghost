namespace Ghost;

public interface IBrowserSession : IAsyncDisposable
{
    string SessionId { get; }
    bool IsConnected { get; }
    IReadOnlyList<IPage> Pages { get; }
    Task<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default);
    Task<IPage?> GetPageAsync(string pageId, CancellationToken ct = default);
    Task CloseAsync(CancellationToken ct = default);
}
