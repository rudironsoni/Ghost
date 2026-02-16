using Ghost.Sdk.Contracts;

namespace Ghost.Platform.Intelligence;

/// <summary>
/// Represents a categorized failure.
/// </summary>
public sealed record Failure(
    string EventId,
    string Kind,
    string FailureCategory,
    string? FailureReason,
    DateTimeOffset TimestampUtc,
    string? CorrelationId,
    string? CausationId,
    IReadOnlyDictionary<string, object?> Data);

/// <summary>
/// Represents the failure analysis view of a run.
/// </summary>
public sealed record FailureAnalysisView(
    string RunId,
    int TotalFailures,
    int NetworkFailures,
    int ParseFailures,
    int ValidationFailures,
    int SystemFailures,
    int UnknownFailures,
    IReadOnlyList<Failure> Failures);

/// <summary>
/// Builds failure analysis views from engine events.
/// </summary>
public sealed class FailureAnalysisViewBuilder
{
    /// <summary>
    /// Builds a failure analysis view from the provided events.
    /// </summary>
    /// <param name="events">The engine events to analyze.</param>
    /// <returns>A failure analysis view of the run.</returns>
    public static async Task<FailureAnalysisView> Build(IAsyncEnumerable<EngineEvent> events)
    {
        List<Failure> failures = [];
        string? runId = null;

        await foreach (var e in events)
        {
            if (runId is null)
            {
                runId = e.RunId;
            }

            if (IsFailureEvent(e))
            {
                var category = ClassifyFailure(e);
                var reason = ExtractFailureReason(e);

                failures.Add(new Failure(
                    e.EventId,
                    e.Kind,
                    category,
                    reason,
                    e.TimestampUtc,
                    e.CorrelationId,
                    e.CausationId,
                    e.Data));
            }
        }

        if (runId is null)
        {
            throw new InvalidOperationException("No events provided to build failure analysis view.");
        }

        var networkFailures = failures.Count(f => f.FailureCategory == "Network");
        var parseFailures = failures.Count(f => f.FailureCategory == "Parse");
        var validationFailures = failures.Count(f => f.FailureCategory == "Validation");
        var systemFailures = failures.Count(f => f.FailureCategory == "System");
        var unknownFailures = failures.Count(f => f.FailureCategory == "Unknown");

        return new FailureAnalysisView(
            runId,
            failures.Count,
            networkFailures,
            parseFailures,
            validationFailures,
            systemFailures,
            unknownFailures,
            failures);
    }

    private static bool IsFailureEvent(EngineEvent e)
    {
        return e.Kind == EventKinds.RunFailed ||
               e.Kind == EventKinds.StepFailed ||
               e.Kind == EventKinds.ItemRejected ||
               e.Kind == EventKinds.CircuitOpened;
    }

    private static string ClassifyFailure(EngineEvent e)
    {
        return e.Kind switch
        {
            EventKinds.CircuitOpened => "Network",
            EventKinds.ItemRejected => "Validation",
            EventKinds.RunFailed => "System",
            EventKinds.StepFailed => ClassifyStepFailure(e),
            _ => "Unknown"
        };
    }

    private static string ClassifyStepFailure(EngineEvent e)
    {
        if (e.Data.TryGetValue("errorType", out var errorType))
        {
            var errorTypeStr = errorType?.ToString()?.ToLowerInvariant();
            if (errorTypeStr is not null)
            {
                if (errorTypeStr.Contains("network") || errorTypeStr.Contains("http") || errorTypeStr.Contains("timeout"))
                {
                    return "Network";
                }
                if (errorTypeStr.Contains("parse") || errorTypeStr.Contains("json") || errorTypeStr.Contains("xml"))
                {
                    return "Parse";
                }
            }
        }

        if (e.Data.TryGetValue("stepKind", out var stepKind))
        {
            var stepKindStr = stepKind?.ToString()?.ToLowerInvariant();
            if (stepKindStr is not null)
            {
                if (stepKindStr.Contains("fetch") || stepKindStr.Contains("request"))
                {
                    return "Network";
                }
                if (stepKindStr.Contains("parse"))
                {
                    return "Parse";
                }
            }
        }

        return "System";
    }

    private static string? ExtractFailureReason(EngineEvent e)
    {
        if (e.Data.TryGetValue("errorMessage", out var errorMessage))
        {
            return errorMessage?.ToString();
        }

        if (e.Data.TryGetValue("reason", out var reason))
        {
            return reason?.ToString();
        }

        if (e.Data.TryGetValue("error", out var error))
        {
            return error?.ToString();
        }

        return null;
    }
}
