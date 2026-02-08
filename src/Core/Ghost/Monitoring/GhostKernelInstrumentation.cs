using System.Diagnostics;
using Ghost.Core;
using Microsoft.Extensions.Logging;

namespace Ghost.Monitoring;

/// <summary>
/// Provides instrumentation helpers for GhostKernel operations.
/// </summary>
public static class GhostKernelInstrumentation
{
    private const string SessionTag = "ghost.session.id";
    private const string OperationTag = "ghost.operation";
    private const string BrowserTag = "ghost.browser";
    private const string StealthTag = "ghost.stealth.enabled";
    private const string ProxyTag = "ghost.proxy.enabled";
    private const string ErrorTag = "ghost.error";

    /// <summary>
    /// Starts an activity for a session creation operation.
    /// </summary>
    public static Activity? StartSessionActivity(string sessionId, bool enableStealth, bool hasProxy)
    {
        var activity = OpenTelemetryConfiguration.ActivitySource.StartActivity(
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
        var activity = OpenTelemetryConfiguration.ActivitySource.StartActivity(
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
        var activity = OpenTelemetryConfiguration.ActivitySource.StartActivity(
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

#pragma warning disable IDE0031 // Null check cannot be simplified here
        if (logger is not null)
#pragma warning restore IDE0031
        {
            // Suppress CA1848 analyzer warning
#pragma warning disable CA1848
            logger.LogError(exception, "Operation failed: {Operation}", activity.DisplayName);
#pragma warning restore CA1848
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
#pragma warning disable IDE0031 // Null check cannot be simplified here
        if (logger is not null)
#pragma warning restore IDE0031
        {
            // Suppress CA1848 analyzer warning
#pragma warning disable CA1848
            logger.LogDebug("Operation completed: {Operation}", activity.DisplayName);
#pragma warning restore CA1848
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

        foreach (var (key, value) in tags)
        {
            activity.SetTag(key, value);
        }
    }
}
