using Ghost.Sdk.Contracts;

namespace Ghost.Platform.Intelligence;

/// <summary>
/// Represents a timeline entry for a run.
/// </summary>
public sealed record TimelineEntry(
    string EventId,
    string Kind,
    DateTimeOffset TimestampUtc,
    string? CorrelationId,
    string? CausationId,
    IReadOnlyDictionary<string, object?> Data);

/// <summary>
/// Represents the complete timeline view of a run.
/// </summary>
public sealed record RunTimelineView(
    string RunId,
    string JobId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool IsCompleted,
    bool IsFailed,
    IReadOnlyList<TimelineEntry> Entries);

/// <summary>
/// Builds a timeline view from engine events.
/// </summary>
public sealed class RunTimelineViewBuilder
{
    /// <summary>
    /// Builds a timeline view from the provided events.
    /// </summary>
    /// <param name="events">The engine events to build the timeline from.</param>
    /// <returns>A timeline view of the run.</returns>
    public static async Task<RunTimelineView> Build(IAsyncEnumerable<EngineEvent> events)
    {
        List<TimelineEntry> entries = [];
        string? runId = null;
        string? jobId = null;
        DateTimeOffset? startedAt = null;
        DateTimeOffset? completedAt = null;
        bool isFailed = false;

        await foreach (var e in events)
        {
            if (runId is null)
            {
                runId = e.RunId;
                jobId = e.JobId;
            }

            if (e.Kind == EventKinds.RunStarted && startedAt is null)
            {
                startedAt = e.TimestampUtc;
            }

            if (e.Kind == EventKinds.RunCompleted && completedAt is null)
            {
                completedAt = e.TimestampUtc;
            }

            if (e.Kind == EventKinds.RunFailed)
            {
                isFailed = true;
                completedAt ??= e.TimestampUtc;
            }

            entries.Add(new TimelineEntry(
                e.EventId,
                e.Kind,
                e.TimestampUtc,
                e.CorrelationId,
                e.CausationId,
                e.Data));
        }

        if (runId is null)
        {
            throw new InvalidOperationException("No events provided to build timeline view.");
        }

        return new RunTimelineView(
            runId,
            jobId ?? string.Empty,
            startedAt ?? DateTimeOffset.MinValue,
            completedAt,
            completedAt is not null,
            isFailed,
            entries);
    }
}
