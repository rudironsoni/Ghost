namespace Ghost.Engine.Abstractions.Transport;

public sealed record ItemEnvelope(
    string Type,
    IReadOnlyDictionary<string, object?> Data,
    DateTimeOffset CapturedAtUtc);
