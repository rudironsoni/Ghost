namespace Ghost;

public interface IBrowserSession : IAsyncDisposable
{
    string SessionId { get; }
    bool IsConnected { get; }
    IReadOnlyList<IPage> Pages { get; }
    ValueTask<IPage> NewPageAsync(PageOptions? options = null, CancellationToken ct = default);
    ValueTask<IPage?> GetPageAsync(string pageId, CancellationToken ct = default);
    ValueTask CloseAsync(CancellationToken ct = default);
}
