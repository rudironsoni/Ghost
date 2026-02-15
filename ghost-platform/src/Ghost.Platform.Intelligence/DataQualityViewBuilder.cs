using Ghost.Sdk.Contracts;

namespace Ghost.Platform.Intelligence;

/// <summary>
/// Represents data quality metrics for a run.
/// </summary>
public sealed record DataQualityMetrics(
    int TotalRequests,
    int SuccessfulResponses,
    int FailedResponses,
    int ItemsEmitted,
    int ItemsRejected,
    int ItemsNormalized,
    int ParseMatches,
    int ParseFailures,
    double SuccessRate,
    double ItemEmissionRate,
    double ItemRejectionRate,
    double ParseSuccessRate);

/// <summary>
/// Represents the data quality view of a run.
/// </summary>
public sealed record DataQualityView(
    string RunId,
    DataQualityMetrics Metrics,
    DateTimeOffset? FirstEventAt,
    DateTimeOffset? LastEventAt);

/// <summary>
/// Builds data quality views from engine events.
/// </summary>
public sealed class DataQualityViewBuilder
{
    /// <summary>
    /// Builds a data quality view from the provided events.
    /// </summary>
    /// <param name="events">The engine events to analyze.</param>
    /// <returns>A data quality view of the run.</returns>
    public static async Task<DataQualityView> Build(IAsyncEnumerable<EngineEvent> events)
    {
        string? runId = null;
        DateTimeOffset? firstEventAt = null;
        DateTimeOffset? lastEventAt = null;

        int totalRequests = 0;
        int successfulResponses = 0;
        int failedResponses = 0;
        int itemsEmitted = 0;
        int itemsRejected = 0;
        int itemsNormalized = 0;
        int parseMatches = 0;
        int parseFailures = 0;

        await foreach (var e in events)
        {
            if (runId is null)
            {
                runId = e.RunId;
            }

            if (firstEventAt is null || e.TimestampUtc < firstEventAt)
            {
                firstEventAt = e.TimestampUtc;
            }

            if (lastEventAt is null || e.TimestampUtc > lastEventAt)
            {
                lastEventAt = e.TimestampUtc;
            }

            switch (e.Kind)
            {
                case EventKinds.RequestSent:
                    totalRequests++;
                    break;

                case EventKinds.ResponseReceived:
                    successfulResponses++;
                    break;

                case EventKinds.StepFailed when IsRequestStep(e):
                    failedResponses++;
                    break;

                case EventKinds.ItemEmitted:
                    itemsEmitted++;
                    break;

                case EventKinds.ItemRejected:
                    itemsRejected++;
                    break;

                case EventKinds.ItemNormalized:
                    itemsNormalized++;
                    break;

                case EventKinds.ParseMatch:
                    parseMatches++;
                    break;

                case EventKinds.StepFailed when IsParseStep(e):
                    parseFailures++;
                    break;
            }
        }

        if (runId is null)
        {
            throw new InvalidOperationException("No events provided to build data quality view.");
        }

        var totalResponses = successfulResponses + failedResponses;
        var successRate = totalResponses > 0 ? (double)successfulResponses / totalResponses : 0.0;
        var totalItems = itemsEmitted + itemsRejected;
        var itemEmissionRate = totalItems > 0 ? (double)itemsEmitted / totalItems : 0.0;
        var itemRejectionRate = totalItems > 0 ? (double)itemsRejected / totalItems : 0.0;
        var totalParseAttempts = parseMatches + parseFailures;
        var parseSuccessRate = totalParseAttempts > 0 ? (double)parseMatches / totalParseAttempts : 0.0;

        var metrics = new DataQualityMetrics(
            totalRequests,
            successfulResponses,
            failedResponses,
            itemsEmitted,
            itemsRejected,
            itemsNormalized,
            parseMatches,
            parseFailures,
            successRate,
            itemEmissionRate,
            itemRejectionRate,
            parseSuccessRate);

        return new DataQualityView(
            runId,
            metrics,
            firstEventAt,
            lastEventAt);
    }

    private static bool IsRequestStep(EngineEvent e)
    {
        if (e.Data.TryGetValue("stepKind", out var stepKind))
        {
            var stepKindStr = stepKind?.ToString()?.ToLowerInvariant();
            return stepKindStr is not null &&
                   (stepKindStr.Contains("fetch") || stepKindStr.Contains("request"));
        }
        return false;
    }

    private static bool IsParseStep(EngineEvent e)
    {
        if (e.Data.TryGetValue("stepKind", out var stepKind))
        {
            var stepKindStr = stepKind?.ToString()?.ToLowerInvariant();
            return stepKindStr is not null && stepKindStr.Contains("parse");
        }
        return false;
    }
}
