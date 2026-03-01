using Microsoft.Playwright;

namespace Ghost.Session;

/// <summary>
/// Manages browser session persistence - saving and restoring cookies, localStorage, and sessionStorage.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Save the current browser context state for a specific platform.
    /// </summary>
    /// <param name="context">The browser context to save.</param>
    /// <param name="platform">Platform identifier (e.g., "LinkedIn").</param>
    /// <param name="sessionId">Optional session ID. If null, generates a new one.</param>
    /// <param name="ttl">Time-to-live for this session. If null, uses default TTL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The session ID of the saved session.</returns>
    public Task<string> SaveSessionAsync(
        IBrowserContext context,
        string platform,
        string? sessionId = null,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Restore a previously saved session state to a browser context.
    /// </summary>
    /// <param name="context">The browser context to restore state to.</param>
    /// <param name="platform">Platform identifier.</param>
    /// <param name="sessionId">Optional specific session ID. If null, uses the latest session for the platform.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if session was restored, false if no session found or expired.</returns>
    public Task<bool> RestoreSessionAsync(
        IBrowserContext context,
        string platform,
        string? sessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Load a session from storage without applying it to a context.
    /// </summary>
    /// <param name="platform">Platform identifier.</param>
    /// <param name="sessionId">Optional specific session ID. If null, loads the latest session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The browser session, or null if not found or expired.</returns>
    public Task<BrowserSession?> LoadSessionAsync(
        string platform,
        string? sessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Delete a specific session or all sessions for a platform.
    /// </summary>
    /// <param name="platform">Platform identifier.</param>
    /// <param name="sessionId">Optional specific session ID. If null, deletes all sessions for the platform.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task DeleteSessionAsync(
        string platform,
        string? sessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// List all session IDs for a specific platform.
    /// </summary>
    /// <param name="platform">Platform identifier.</param>
    /// <param name="includeExpired">Whether to include expired sessions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of session IDs.</returns>
    public Task<List<string>> ListSessionsAsync(
        string platform,
        bool includeExpired = false,
        CancellationToken ct = default);

    /// <summary>
    /// Clean up expired sessions for all platforms.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of sessions deleted.</returns>
    public Task<int> CleanupExpiredSessionsAsync(CancellationToken ct = default);
}
