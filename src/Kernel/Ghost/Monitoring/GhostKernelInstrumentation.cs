using System.Diagnostics;
using Ghost.Kernel;
using Microsoft.Extensions.Logging;

namespace Ghost.Monitoring;

/// <summary>
/// Provides instrumentation helpers for GhostKernel operations.
/// </summary>
public static partial class GhostKernelInstrumentation
{
    // LoggerMessage source generators (EventIds 4000-4099 for Monitoring)
    [LoggerMessage(EventId = 4000, Level = LogLevel.Error, Message = "Operation failed: {Operation}")]
    private static partial void LogOperationFailed(ILogger logger, Exception ex, string operation);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Debug, Message = "Operation completed: {Operation}")]
    private static partial void LogOperationCompleted(ILogger logger, string operation);
    private const string SessionTag = "ghost.session.id";
    private const string OperationTag = "ghost.operation";
    private const string StealthTag = "ghost.stealth.enabled";
    private const string ProxyTag = "ghost.proxy.enabled";
    private const string ErrorTag = "ghost.error";

    /// <summary>
    /// Starts an activity for a session creation operation.
    /// </summary>
    public static Activity? StartSessionActivity(string sessionId, bool enableStealth, bool hasProxy)
    {
        Activity? activity = OpenTelemetryConfiguration.ActivitySource.StartActivity(
            "GhostKernel.NewSession",
            ActivityKind.Internal);

        activity?.SetTag(SessionTag, sessionId);
        activity?.SetTag(OperationTag, "NewSession");
        activity?.SetTag(StealthTag, enableStealth);
        activity?.SetTag(ProxyTag, hasProxy);

        return activity;
    }

    /// <summary>
    /// Starts an activity for a navigation operation.
    /// </summary>
    public static Activity? StartNavigationActivity(string sessionId, string url)
    {
        Activity? activity = OpenTelemetryConfiguration.ActivitySource.StartActivity(
            "GhostKernel.Navigate",
            ActivityKind.Internal);

        activity?.SetTag(SessionTag, sessionId);
        activity?.SetTag(OperationTag, "Navigate");
        activity?.SetTag("ghost.url", url);

        return activity;
    }

    /// <summary>
    /// Starts an activity for a scraping operation.
    /// </summary>
    public static Activity? StartScrapingActivity(string sessionId, string platform, string operation)
    {
        Activity? activity = OpenTelemetryConfiguration.ActivitySource.StartActivity(
            $"GhostKernel.Scrape.{platform}",
            ActivityKind.Internal);

        activity?.SetTag(SessionTag, sessionId);
        activity?.SetTag(OperationTag, operation);
        activity?.SetTag("ghost.platform", platform);

        return activity;
    }

    /// <summary>
    /// Records an error on the current activity.
    /// </summary>
    public static void RecordError(Activity? activity, Exception exception, ILogger? logger = null)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag(ErrorTag, exception.GetType().Name);

        // Manually add exception details as tags (RecordException is not available in all versions)
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stacktrace", exception.StackTrace);

        if (logger is not null)
        {
            LogOperationFailed(logger, exception, activity.DisplayName);
        }
    }

    /// <summary>
    /// Completes an activity with success status.
    /// </summary>
    public static void CompleteActivity(Activity? activity, ILogger? logger = null)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Ok);
        if (logger is not null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                LogOperationCompleted(logger, activity.DisplayName);
            }
        }
        activity.Dispose();
    }

    /// <summary>
    /// Adds correlation ID to the current activity.
    /// </summary>
    public static void AddCorrelationId(Activity? activity, string correlationId)
    {
        activity?.SetTag("ghost.correlation.id", correlationId);
    }

    /// <summary>
    /// Adds custom tags to the current activity.
    /// </summary>
    public static void AddTags(Activity? activity, Dictionary<string, object?> tags)
    {
        if (activity is null || tags is null)
        {
            return;
        }

        foreach ((string? key, object? value) in tags)
        {
            activity.SetTag(key, value);
        }
    }
}
