using System.Text.Json;

namespace Ghost.Platform.Fetchers;

public interface IFetcher
{
    Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken ct);
}

public sealed record FetchRequest(
    string Url,
    string Method,
    Dictionary<string, string> Headers,
    byte[]? Body,
    JsonDocument? ReplayKey,
    TimeSpan Timeout);

public sealed record FetchResult(
    int StatusCode,
    Dictionary<string, string> Headers,
    byte[] Body,
    string? ArtifactKey);
