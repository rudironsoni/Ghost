using Ghost.Sdk.Contracts;

namespace Ghost.Platform.Events;

public interface IRunEventStore
{
    ValueTask AppendAsync(EngineEvent e, CancellationToken ct);
    IAsyncEnumerable<EngineEvent> ReadAsync(string runId, CancellationToken ct);
    ValueTask<long> GetCountAsync(string runId, CancellationToken ct);
}
