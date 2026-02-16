using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Platform.Storage.Session;

/// <summary>
/// Session health status
/// </summary>
public enum SessionHealth
{
    /// <summary>Session is healthy and ready for use</summary>
    Healthy,
    /// <summary>Session is degraded but usable</summary>
    Degraded,
    /// <summary>Session is unhealthy and should be recycled</summary>
    Unhealthy
}

/// <summary>
/// Session type enumeration
/// </summary>
public enum SessionType
{
    /// <summary>HTTP session with rotating proxies</summary>
    Http,
    /// <summary>Browser session from tiered pool</summary>
    Browser
}

/// <summary>
/// Session allocation context for intelligent routing
/// </summary>
public record SessionAllocationContext(
    string PlatformName,
    string? CountryCode,
    SessionType SessionType,
    int? ComplexityScore = null,
    Dictionary<string, string>? Metadata = null
);

/// <summary>
/// Session health metrics
/// </summary>
public record SessionHealthMetrics(
    string SessionId,
    SessionHealth Health,
    DateTime LastChecked,
    int SuccessfulRequests,
    int FailedRequests,
    TimeSpan Uptime,
    Dictionary<string, object>? AdditionalMetrics = null
);

/// <summary>
/// Session affinity options
/// </summary>
public record SessionAffinityOptions(
    string AffinityKey,
    TimeSpan? AffinityDuration = null,
    bool AllowFallback = true
);

/// <summary>
/// Orchestrates session allocation, lifecycle management, and health monitoring
/// for both HTTP and Browser sessions with intelligent routing capabilities.
/// </summary>
public interface ISessionOrchestrator
{
    /// <summary>
    /// Allocates a session based on context-aware routing.
    /// Intelligently selects between HTTP and Browser sessions based on platform needs,
    /// geographic requirements, and complexity scores.
    /// </summary>
    /// <param name="context">Allocation context containing routing information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Session identifier</returns>
    public Task<string> AllocateSessionAsync(SessionAllocationContext context, CancellationToken ct = default);

    /// <summary>
    /// Allocates a session with affinity for consistent routing.
    /// Ensures subsequent requests with the same affinity key are routed to the same session.
    /// </summary>
    /// <param name="context">Allocation context</param>
    /// <param name="affinityOptions">Affinity configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Session identifier</returns>
    public Task<string> AllocateSessionWithAffinityAsync(
        SessionAllocationContext context,
        SessionAffinityOptions affinityOptions,
        CancellationToken ct = default);

    /// <summary>
    /// Gets an HTTP session for making HTTP requests with proxy rotation.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>HTTP session instance or null if not found</returns>
    public Task<RotatingProxySession?> GetHttpSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Gets a browser session for browser automation.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Browser session instance or null if not found</returns>
    public Task<IBrowserSession?> GetBrowserSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Monitors and reports session health status.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Health metrics for the session</returns>
    public Task<SessionHealthMetrics> GetSessionHealthAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Monitors health of all active sessions.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of health metrics for all sessions</returns>
    public Task<IReadOnlyList<SessionHealthMetrics>> GetAllSessionHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Recycles an unhealthy session by closing and removing it from the pool.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="ct">Cancellation token</param>
    public Task RecycleSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Extends the time-to-live (TTL) for a session to keep it alive longer.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="additionalTime">Additional time to add to TTL</param>
    /// <param name="ct">Cancellation token</param>
    public Task ExtendSessionTtlAsync(string sessionId, TimeSpan additionalTime, CancellationToken ct = default);

    /// <summary>
    /// Persists session state to storage for recovery.
    /// Useful for browser sessions that need to maintain authentication and state.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="storagePath">Path to persist session state</param>
    /// <param name="ct">Cancellation token</param>
    public Task PersistSessionStateAsync(string sessionId, string storagePath, CancellationToken ct = default);

    /// <summary>
    /// Restores a session from persisted state.
    /// </summary>
    /// <param name="storagePath">Path to persisted session state</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Restored session identifier</returns>
    public Task<string> RestoreSessionFromStateAsync(string storagePath, CancellationToken ct = default);

    /// <summary>
    /// Gets all active session identifiers.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of active session identifiers</returns>
    public Task<IReadOnlyList<string>> GetActiveSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all active session identifiers of a specific type.
    /// </summary>
    /// <param name="sessionType">Type of sessions to retrieve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of active session identifiers</returns>
    public Task<IReadOnlyList<string>> GetActiveSessionsByTypeAsync(SessionType sessionType, CancellationToken ct = default);

    /// <summary>
    /// Closes a session and releases its resources.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="ct">Cancellation token</param>
    public Task CloseSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Performs health check sweep on all sessions and recycles unhealthy ones.
    /// Should be called periodically by background maintenance tasks.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of sessions recycled</returns>
    public Task<int> PerformHealthCheckSweepAsync(CancellationToken ct = default);
}
