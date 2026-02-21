namespace Ghost.Cloud.Contracts.Runs;

/// <summary>
/// Classification types for authorization decisions to enable filtering and analysis.
/// </summary>
public enum AuthorizationDecisionClassification
{
    /// <summary>Request was authorized successfully.</summary>
    Authorized,

    /// <summary>Request was denied due to quota limits.</summary>
    QuotaExceeded,

    /// <summary>Request was denied due to authorization rules.</summary>
    Unauthorized,

    /// <summary>Request was denied due to validation failure.</summary>
    ValidationFailed,

    /// <summary>Request was already authorized (idempotent).</summary>
    Idempotent
}

[GenerateSerializer]
public sealed record RunAuthorizationDecision
{
    [Id(0)] public bool IsAuthorized { get; init; }
    [Id(1)] public string Code { get; init; } = string.Empty;
    [Id(2)] public string Message { get; init; } = string.Empty;
    [Id(3)] public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
    [Id(4)] public int CurrentRunCount { get; init; }
    [Id(5)] public int ActiveRunCount { get; init; }
    [Id(6)] public int DailyRunLimit { get; init; }
    [Id(7)] public int MaxConcurrentRuns { get; init; }

    // CL-002: Enhanced auditability fields
    [Id(8)] public AuthorizationDecisionClassification Classification { get; init; }

    /// <summary>
    /// URI to verification evidence/diagnostics for this decision.
    /// Populated for denied requests to enable troubleshooting.
    /// </summary>
    [Id(9)] public string? VerificationEvidenceUri { get; init; }

    /// <summary>
    /// Tenant ID for cross-reference in audit logs.
    /// </summary>
    [Id(10)] public Guid TenantId { get; init; }
}

[GenerateSerializer]
public sealed record RunAuthorizationAuditEntry
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public string EndpointId { get; init; } = string.Empty;
    [Id(2)] public RunAuthorizationDecision Decision { get; init; } = new();

    // CL-002: Request metadata for comprehensive auditing
    [Id(3)] public string? RequestIpAddress { get; init; }
    [Id(4)] public string? RequestCorrelationId { get; init; }
    [Id(5)] public DateTimeOffset RequestTimestamp { get; init; } = DateTimeOffset.UtcNow;
}
