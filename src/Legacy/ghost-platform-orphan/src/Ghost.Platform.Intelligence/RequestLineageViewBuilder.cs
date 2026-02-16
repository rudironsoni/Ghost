using Ghost.Sdk.Contracts;

namespace Ghost.Platform.Intelligence;

/// <summary>
/// Represents a single step in the request lineage.
/// </summary>
public sealed record LineageStep(
    string EventId,
    string Kind,
    DateTimeOffset TimestampUtc,
    IReadOnlyDictionary<string, object?> Data);

/// <summary>
/// Represents the complete lineage of a request through the system.
/// </summary>
public sealed record RequestLineage(
    string RequestId,
    string? CorrelationId,
    DateTimeOffset RequestSentAt,
    DateTimeOffset? ResponseReceivedAt,
    DateTimeOffset? ParseMatchedAt,
    DateTimeOffset? ItemEmittedAt,
    bool IsCompleted,
    bool IsFailed,
    IReadOnlyList<LineageStep> Steps);

/// <summary>
/// Builds request lineage views from engine events.
/// </summary>
public sealed class RequestLineageViewBuilder
{
    /// <summary>
    /// Builds request lineage views from the provided events.
    /// </summary>
    /// <param name="events">The engine events to build lineage from.</param>
    /// <returns>A read-only list of request lineage views.</returns>
    public static async Task<IReadOnlyList<RequestLineage>> Build(IAsyncEnumerable<EngineEvent> events)
    {
        var lineageMap = new Dictionary<string, List<LineageStep>>();

        await foreach (var e in events)
        {
            // Track events that are part of request lineage
            if (IsLineageEvent(e))
            {
                var requestId = GetRequestId(e);
                if (requestId is not null)
                {
                    if (!lineageMap.TryGetValue(requestId, out var steps))
                    {
                        steps = [];
                        lineageMap[requestId] = steps;
                    }

                    steps.Add(new LineageStep(
                        e.EventId,
                        e.Kind,
                        e.TimestampUtc,
                        e.Data));
                }
            }
        }

        List<RequestLineage> result = [];

        foreach (var (requestId, steps) in lineageMap)
        {
            var sortedSteps = steps.OrderBy(s => s.TimestampUtc).ToList();
            var firstStep = sortedSteps.FirstOrDefault();
            var correlationId = firstStep?.Data.GetValueOrDefault("correlationId")?.ToString();

            var requestSentAt = sortedSteps.FirstOrDefault(s => s.Kind == EventKinds.RequestSent)?.TimestampUtc;
            var responseReceivedAt = sortedSteps.FirstOrDefault(s => s.Kind == EventKinds.ResponseReceived)?.TimestampUtc;
            var parseMatchedAt = sortedSteps.FirstOrDefault(s => s.Kind == EventKinds.ParseMatch)?.TimestampUtc;
            var itemEmittedAt = sortedSteps.FirstOrDefault(s => s.Kind == EventKinds.ItemEmitted)?.TimestampUtc;

            var isCompleted = itemEmittedAt is not null;
            var isFailed = sortedSteps.Any(s => s.Kind == EventKinds.StepFailed);

            result.Add(new RequestLineage(
                requestId,
                correlationId,
                requestSentAt ?? DateTimeOffset.MinValue,
                responseReceivedAt,
                parseMatchedAt,
                itemEmittedAt,
                isCompleted,
                isFailed,
                sortedSteps));
        }

        return result;
    }

    private static bool IsLineageEvent(EngineEvent e)
    {
        return e.Kind == EventKinds.RequestSent ||
               e.Kind == EventKinds.ResponseReceived ||
               e.Kind == EventKinds.ParseMatch ||
               e.Kind == EventKinds.ItemEmitted ||
               e.Kind == EventKinds.ItemRejected ||
               e.Kind == EventKinds.StepFailed;
    }

    private static string? GetRequestId(EngineEvent e)
    {
        if (e.Data.TryGetValue("requestId", out var requestId))
        {
            return requestId?.ToString();
        }

        // Try correlation ID as fallback
        if (!string.IsNullOrEmpty(e.CorrelationId))
        {
            return e.CorrelationId;
        }

        return null;
    }
}
