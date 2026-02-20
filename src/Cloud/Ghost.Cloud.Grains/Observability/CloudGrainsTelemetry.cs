using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ghost.Cloud.Grains.Observability;

public static class CloudGrainsTelemetry
{
    public const string ActivitySourceName = "Ghost.Cloud.Grains";
    public const string MeterName = "Ghost.Cloud.Grains";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> RunTriggerCounter = Meter.CreateCounter<long>(
        "ghost_cloud_grain_run_trigger_total",
        unit: "runs");

    private static readonly Counter<long> RunTriggerFailureCounter = Meter.CreateCounter<long>(
        "ghost_cloud_grain_run_trigger_failures_total",
        unit: "runs");

    private static readonly Counter<long> AuthorizationDecisionCounter = Meter.CreateCounter<long>(
        "ghost_cloud_grain_authorization_decisions_total",
        unit: "decisions");

    private static readonly Counter<long> ScheduledOperationCounter = Meter.CreateCounter<long>(
        "ghost_cloud_grain_scheduled_operations_total",
        unit: "operations");

    private static readonly Histogram<double> RunDurationHistogram = Meter.CreateHistogram<double>(
        "ghost_cloud_grain_run_duration_seconds",
        unit: "s");

    public static void RecordRunTrigger(string mode)
    {
        RunTriggerCounter.Add(1, new KeyValuePair<string, object?>("mode", mode));
    }

    public static void RecordRunTriggerFailure(string reason, string mode)
    {
        RunTriggerFailureCounter.Add(
            1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("mode", mode));
    }

    public static void RecordAuthorizationDecision(string code, bool isAuthorized)
    {
        AuthorizationDecisionCounter.Add(
            1,
            new KeyValuePair<string, object?>("code", code),
            new KeyValuePair<string, object?>("authorized", isAuthorized));
    }

    public static void RecordScheduledOperation(string operation, string status)
    {
        ScheduledOperationCounter.Add(
            1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", status));
    }

    public static void RecordRunDuration(double durationSeconds, string status)
    {
        RunDurationHistogram.Record(
            durationSeconds,
            new KeyValuePair<string, object?>("status", status));
    }
}
