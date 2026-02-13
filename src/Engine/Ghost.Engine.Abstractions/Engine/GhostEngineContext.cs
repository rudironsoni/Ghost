namespace Ghost.Engine.Abstractions.Engine;

public sealed record GhostEngineContext(
    string JobId,
    string SpiderName,
    IReadOnlyDictionary<string, object?> Metadata);
