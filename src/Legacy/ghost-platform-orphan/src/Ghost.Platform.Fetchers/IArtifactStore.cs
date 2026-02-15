namespace Ghost.Platform.Fetchers;

public interface IArtifactStore
{
    Task<Artifact> GetAsync(string key, CancellationToken ct);
    Task StoreAsync(string key, Artifact artifact, CancellationToken ct);
}

public sealed record Artifact(
    string Key,
    string ContentType,
    byte[] Bytes);
