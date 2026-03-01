using Ghost.Cloud.Contracts.Delivery;

namespace Ghost.Cloud.Contracts.Runs;

/// <summary>
/// Metadata for canary runs.
/// </summary>
[GenerateSerializer]
public sealed record CanaryMetadata
{
    /// <summary>The endpoint being canaried.</summary>
    [Id(0)] public string EndpointId { get; init; } = string.Empty;

    /// <summary>Expected classification outcome (e.g., "Success", "Healthy").</summary>
    [Id(1)] public string? ExpectedOutcome { get; init; }

    /// <summary>Timeout for the canary run in seconds.</summary>
    [Id(2)] public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Whether to capture diagnostics on failure.</summary>
    [Id(3)] public bool CaptureDiagnostics { get; init; } = true;
}

/// <summary>
/// Metadata for replay runs.
/// </summary>
[GenerateSerializer]
public sealed record ReplayMetadata
{
    /// <summary>The cassette key to replay from.</summary>
    [Id(0)] public string CassetteKey { get; init; } = string.Empty;

    /// <summary>Timestamp of the original recording.</summary>
    [Id(1)] public DateTimeOffset? OriginalTimestamp { get; init; }

    /// <summary>Whether to validate responses against original.</summary>
    [Id(2)] public bool ValidateAgainstOriginal { get; init; } = true;

    /// <summary>Allowed variance for response comparison (0.0 = exact match).</summary>
    [Id(3)] public double AllowedVariance { get; init; } = 0.0;
}

/// <summary>
/// Metadata for cassette refresh runs.
/// </summary>
[GenerateSerializer]
public sealed record CassetteRefreshMetadata
{
    /// <summary>The cassette key to refresh.</summary>
    [Id(0)] public string CassetteKey { get; init; } = string.Empty;

    /// <summary>Reason for refresh (e.g., "expired", "schema_change").</summary>
    [Id(1)] public string RefreshReason { get; init; } = string.Empty;

    /// <summary>Whether to redact sensitive data during refresh.</summary>
    [Id(2)] public bool RedactSensitiveData { get; init; } = true;

    /// <summary>Previous cassette version for rollback.</summary>
    [Id(3)] public string? PreviousVersion { get; init; }
}

[GenerateSerializer]
public sealed record ScrapeRunRequest
{
    [Id(0)] public string EndpointId { get; init; } = string.Empty;
    [Id(1)] public JsonElement Input { get; init; }
    [Id(2)] public DeliveryConfig? Delivery { get; init; }
    [Id(3)] public string? IdempotencyKey { get; init; }
    [Id(4)] public string RequestedMode { get; init; } = "async";
    [Id(5)] public required Guid TenantId { get; init; }

    // CL-004: Run metadata for canary/replay/refresh
    [Id(6)] public string RunKind { get; init; } = "canary";
    [Id(7)] public CanaryMetadata? CanaryMetadata { get; init; }
    [Id(8)] public ReplayMetadata? ReplayMetadata { get; init; }
    [Id(9)] public CassetteRefreshMetadata? CassetteRefreshMetadata { get; init; }
}
