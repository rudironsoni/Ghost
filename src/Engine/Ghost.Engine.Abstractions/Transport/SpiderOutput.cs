namespace Ghost.Engine.Abstractions.Transport;

public sealed record SpiderOutput(
    IReadOnlyList<GhostRequest> Requests,
    IReadOnlyList<ItemEnvelope> Items);
