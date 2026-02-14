using System.Text.Json;

namespace Ghost.Sdk.Contracts;

/// <summary>
/// Definition of a job to be executed.
/// </summary>
public sealed record JobDefinition(
    string JobId,
    PluginId PluginId,
    SpiderId SpiderId,
    JsonDocument Input,
    JobBudgets Budgets,
    JobTraceContext TraceContext);

/// <summary>
/// Budget constraints for a job execution.
/// </summary>
public sealed record JobBudgets(
    TimeSpan MaxDuration,
    int MaxRequests,
    int MaxItems,
    int MaxDepth);

/// <summary>
/// Trace context for distributed tracing and correlation.
/// </summary>
public sealed record JobTraceContext(
    string CorrelationId,
    string? CausationId,
    string? TraceParent,
    IReadOnlyDictionary<string, string> Baggage);
