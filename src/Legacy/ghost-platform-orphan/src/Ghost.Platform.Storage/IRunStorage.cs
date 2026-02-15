using System.Text.Json;

namespace Ghost.Platform.Storage;

public interface IRunStorage
{
    IDatasetStore GetDatasetStore(string name);
    IKeyValueStore GetKeyValueStore(string name);
    IRequestQueueStore GetRequestQueueStore(string name);
}

public interface IDatasetStore
{
    Task AppendAsync(string itemType, JsonDocument item, CancellationToken ct = default);
    IAsyncEnumerable<(string ItemType, JsonDocument Item)> ReadAsync(CancellationToken ct = default);
}

public interface IKeyValueStore
{
    Task SetAsync(string key, byte[] data, string contentType, CancellationToken ct = default);
    Task<(byte[] Data, string ContentType)?> GetAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public interface IRequestQueueStore
{
    Task EnqueueAsync(FetchRequest request, CancellationToken ct = default);
    Task<FetchRequest?> DequeueAsync(CancellationToken ct = default);
    Task MarkAsHandledAsync(string requestId, CancellationToken ct = default);
    Task<bool> IsHandledAsync(string requestId, CancellationToken ct = default);
}

public sealed record FetchRequest(
    string RequestId,
    string Url,
    string Method,
    Dictionary<string, string> Headers,
    byte[]? Body);
