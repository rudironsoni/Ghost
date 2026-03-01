namespace Ghost.Engine.Abstractions.Transport;

public sealed record GhostRequest(
    string Url,
    string Method,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    TimeSpan? Timeout);
