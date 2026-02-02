using System;
using System.ComponentModel.DataAnnotations;

namespace Ghost.Platform.Common.Session;

/// <summary>
/// Configuration options for SessionOrchestrator.
/// Controls session lifecycle, health monitoring, affinity behavior, and complexity-based routing.
/// </summary>
public sealed class SessionOrchestratorOptions
{
    /// <summary>
    /// Default time-to-live for allocated sessions.
    /// Sessions exceeding this duration without activity will be eligible for recycling.
    /// </summary>
    public TimeSpan DefaultSessionTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Interval between health check sweeps on all active sessions.
    /// The orchestrator will periodically check session health and recycle unhealthy ones.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum duration for session affinity bindings.
    /// After this time, affinity mappings expire and new sessions may be allocated.
    /// </summary>
    public TimeSpan MaxAffinityDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Default affinity duration when not explicitly specified in allocation requests.
    /// </summary>
    public TimeSpan DefaultAffinityDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Complexity score threshold for routing to browser sessions.
    /// Requests with complexity scores at or above this threshold will prefer browser sessions.
    /// Scores below this threshold will prefer HTTP sessions for better performance.
    /// </summary>
    [Range(0, 100)]
    public int BrowserSessionComplexityThreshold { get; set; } = 70;

    /// <summary>
    /// Maximum number of concurrent HTTP sessions to maintain in the pool.
    /// </summary>
    [Range(1, 1000)]
    public int MaxConcurrentHttpSessions { get; set; } = 50;

    /// <summary>
    /// Maximum number of concurrent browser sessions to maintain in the pool.
    /// Browser sessions are more resource-intensive than HTTP sessions.
    /// </summary>
    [Range(1, 500)]
    public int MaxConcurrentBrowserSessions { get; set; } = 20;

    /// <summary>
    /// Timeout for acquiring a session from the pool.
    /// If a session cannot be allocated within this time, the request fails.
    /// </summary>
    public TimeSpan SessionAcquisitionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Enable automatic session recycling when health checks detect unhealthy sessions.
    /// When disabled, unhealthy sessions must be manually recycled.
    /// </summary>
    public bool EnableAutoRecycling { get; set; } = true;

    /// <summary>
    /// Enable session affinity support.
    /// When disabled, affinity requests are treated as regular allocations.
    /// </summary>
    public bool EnableSessionAffinity { get; set; } = true;

    /// <summary>
    /// Enable complexity-based routing for intelligent session type selection.
    /// When disabled, session type must be explicitly specified in allocation context.
    /// </summary>
    public bool EnableComplexityRouting { get; set; } = true;

    /// <summary>
    /// Threshold for failed requests before marking an HTTP session as unhealthy.
    /// </summary>
    [Range(1, 100)]
    public int HttpSessionFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Threshold for failed requests before marking a browser session as unhealthy.
    /// </summary>
    [Range(1, 100)]
    public int BrowserSessionFailureThreshold { get; set; } = 3;

    /// <summary>
    /// Window of time to track failures for health status determination.
    /// Failures outside this window are not counted toward failure thresholds.
    /// </summary>
    public TimeSpan FailureTrackingWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Enable session state persistence for recovery across restarts.
    /// When enabled, browser session state can be saved and restored.
    /// </summary>
    public bool EnableStatePersistence { get; set; } = true;

    /// <summary>
    /// Directory path for persisting session state.
    /// Must be writable by the application process.
    /// </summary>
    public string StatePersistencePath { get; set; } = ".ghost/sessions";

    /// <summary>
    /// Enable detailed health metrics collection.
    /// When disabled, only basic health status is tracked for better performance.
    /// </summary>
    public bool EnableDetailedHealthMetrics { get; set; } = true;

    /// <summary>
    /// Maximum number of sessions to keep in affinity cache.
    /// Older affinity mappings are evicted when this limit is reached.
    /// </summary>
    [Range(10, 10000)]
    public int MaxAffinityCacheSize { get; set; } = 1000;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="ValidationException">Thrown when configuration is invalid</exception>
    public void Validate()
    {
        if (DefaultSessionTtl <= TimeSpan.Zero)
            throw new ValidationException($"{nameof(DefaultSessionTtl)} must be greater than zero");

        if (HealthCheckInterval <= TimeSpan.Zero)
            throw new ValidationException($"{nameof(HealthCheckInterval)} must be greater than zero");

        if (MaxAffinityDuration <= TimeSpan.Zero)
            throw new ValidationException($"{nameof(MaxAffinityDuration)} must be greater than zero");

        if (DefaultAffinityDuration <= TimeSpan.Zero)
            throw new ValidationException($"{nameof(DefaultAffinityDuration)} must be greater than zero");

        if (DefaultAffinityDuration > MaxAffinityDuration)
            throw new ValidationException($"{nameof(DefaultAffinityDuration)} cannot exceed {nameof(MaxAffinityDuration)}");

        if (SessionAcquisitionTimeout <= TimeSpan.Zero)
            throw new ValidationException($"{nameof(SessionAcquisitionTimeout)} must be greater than zero");

        if (FailureTrackingWindow <= TimeSpan.Zero)
            throw new ValidationException($"{nameof(FailureTrackingWindow)} must be greater than zero");

        if (string.IsNullOrWhiteSpace(StatePersistencePath))
            throw new ValidationException($"{nameof(StatePersistencePath)} cannot be null or empty");

        if (BrowserSessionComplexityThreshold < 0 || BrowserSessionComplexityThreshold > 100)
            throw new ValidationException($"{nameof(BrowserSessionComplexityThreshold)} must be between 0 and 100");

        if (MaxConcurrentHttpSessions < 1)
            throw new ValidationException($"{nameof(MaxConcurrentHttpSessions)} must be at least 1");

        if (MaxConcurrentBrowserSessions < 1)
            throw new ValidationException($"{nameof(MaxConcurrentBrowserSessions)} must be at least 1");

        if (HttpSessionFailureThreshold < 1)
            throw new ValidationException($"{nameof(HttpSessionFailureThreshold)} must be at least 1");

        if (BrowserSessionFailureThreshold < 1)
            throw new ValidationException($"{nameof(BrowserSessionFailureThreshold)} must be at least 1");

        if (MaxAffinityCacheSize < 10)
            throw new ValidationException($"{nameof(MaxAffinityCacheSize)} must be at least 10");
    }
}
