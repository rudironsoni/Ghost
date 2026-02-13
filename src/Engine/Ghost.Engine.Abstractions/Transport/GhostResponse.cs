namespace Ghost.Engine.Abstractions.Transport;

public sealed record GhostResponse(
    string Url,
    int StatusCode,
    IReadOnlyDictionary<string, string> Headers,
    string? Content,
    DateTimeOffset ReceivedAtUtc);
