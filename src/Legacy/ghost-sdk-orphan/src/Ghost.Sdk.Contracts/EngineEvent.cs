using System.Text.Json;

namespace Ghost.Sdk.Contracts;

/// <summary>
/// Canonical event envelope for all engine events.
/// </summary>
public sealed record EngineEvent(
    string EventId,
    int SchemaVersion,
    string RunId,
    string JobId,
    string Kind,
    DateTimeOffset TimestampUtc,
    string CorrelationId,
    string? CausationId,
    string? TraceParent,
    IReadOnlyDictionary<string, string> Baggage,
    IReadOnlyDictionary<string, object?> Data);

/// <summary>
/// Known event kind constants.
/// </summary>
public static class EventKinds
{
    public const string RunStarted = "run_started";
    public const string RunCompleted = "run_completed";
    public const string RunFailed = "run_failed";
    public const string StepStarted = "step_started";
    public const string StepCompleted = "step_completed";
    public const string StepFailed = "step_failed";
    public const string RequestEnqueued = "request_enqueued";
    public const string RequestDequeued = "request_dequeued";
    public const string RequestSent = "request_sent";
    public const string ResponseReceived = "response_received";
    public const string RetryScheduled = "retry_scheduled";
    public const string CircuitOpened = "circuit_opened";
    public const string CircuitClosed = "circuit_closed";
    public const string ArtifactCaptured = "artifact_captured";
    public const string ParseMatch = "parse_match";
    public const string ItemEmitted = "item_emitted";
    public const string ItemRejected = "item_rejected";
    public const string ItemNormalized = "item_normalized";
    public const string BudgetExceeded = "budget_exceeded";
}
