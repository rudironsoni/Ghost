using Ghost.Cloud.Contracts.Runs;

namespace Ghost.Cloud.Contracts.Events;

[GenerateSerializer]
public abstract record ScrapeRunEvent;

[GenerateSerializer]
public record ScrapeRunTriggered(
    [property: Id(0)] string RunId,
    [property: Id(1)] string EndpointId,
    [property: Id(2)] Guid TenantId,
    [property: Id(3)] string Mode,
    [property: Id(4)] DateTimeOffset Timestamp,
    // CL-004: Run metadata for canary/replay/refresh
    [property: Id(5)] string RunKind = "canary",
    [property: Id(6)] CanaryMetadata? CanaryMetadata = null,
    [property: Id(7)] ReplayMetadata? ReplayMetadata = null,
    [property: Id(8)] CassetteRefreshMetadata? CassetteRefreshMetadata = null
) : ScrapeRunEvent;

[GenerateSerializer]
public record ScrapeRunStarted(
    [property: Id(0)] string RunId,
    [property: Id(1)] string WorkerId,
    [property: Id(2)] DateTimeOffset Timestamp
) : ScrapeRunEvent;

[GenerateSerializer]
public record ItemDiscovered(
    [property: Id(0)] string RunId,
    [property: Id(1)] string ItemId,
    [property: Id(2)] JsonElement Data,
    [property: Id(3)] DateTimeOffset Timestamp
) : ScrapeRunEvent;

[GenerateSerializer]
public record ArtifactCaptured(
    [property: Id(0)] string RunId,
    [property: Id(1)] string ItemId,
    [property: Id(2)] string ArtifactType,
    [property: Id(3)] string StorageUri,
    [property: Id(4)] string Hash,
    [property: Id(5)] DateTimeOffset Timestamp
) : ScrapeRunEvent;

[GenerateSerializer]
public record ScrapeRunCompleted(
    [property: Id(0)] string RunId,
    [property: Id(1)] int ItemsDiscovered,
    [property: Id(2)] int ArtifactsCaptured,
    [property: Id(3)] DateTimeOffset Timestamp
) : ScrapeRunEvent;

[GenerateSerializer]
public record ScrapeRunFailed(
    [property: Id(0)] string RunId,
    [property: Id(1)] string ErrorCode,
    [property: Id(2)] string ErrorMessage,
    [property: Id(3)] bool Retryable,
    [property: Id(4)] DateTimeOffset Timestamp
) : ScrapeRunEvent;
