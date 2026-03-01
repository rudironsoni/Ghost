namespace Ghost.Cloud.Delivery.Sinks;

public interface IResultSink
{
    public string Type { get; }
    public Task<SinkResult> WriteBatchAsync(List<JsonElement> items, string? cursor, CancellationToken ct);
    public Task<SinkResult> CompleteAsync(CancellationToken ct);
}

public record SinkResult
{
    public bool Success { get; init; }
    public string? Cursor { get; init; }
    public long BytesWritten { get; init; }
    public string? ErrorMessage { get; init; }
}
