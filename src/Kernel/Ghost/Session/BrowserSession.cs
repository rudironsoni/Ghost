using Microsoft.Playwright;

namespace Ghost.Session;

/// <summary>
/// Represents a persisted browser session state.
/// </summary>
public sealed class BrowserSession
{
    /// <summary>
    /// Unique identifier for this session.
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// Platform identifier (e.g., "LinkedIn", "Indeed", "Glassdoor").
    /// </summary>
    public required string Platform { get; set; }

    /// <summary>
    /// Browser cookies for this session.
    /// </summary>
    public required List<BrowserContextCookiesResult> Cookies { get; set; }

    /// <summary>
    /// LocalStorage key-value pairs.
    /// </summary>
    public required Dictionary<string, string> LocalStorage { get; set; }

    /// <summary>
    /// SessionStorage key-value pairs.
    /// </summary>
    public required Dictionary<string, string> SessionStorage { get; set; }

    /// <summary>
    /// When this session was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this session expires.
    /// </summary>
    public required DateTime ExpiresAt { get; set; }

    /// <summary>
    /// User agent used for this session.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Viewport dimensions used for this session.
    /// </summary>
    public ViewportDimensions? Viewport { get; set; }

    /// <summary>
    /// Timezone used for this session.
    /// </summary>
    public string? TimezoneId { get; set; }

    /// <summary>
    /// Locale used for this session.
    /// </summary>
    public string? Locale { get; set; }

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserSession"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider to use for time-based operations. Defaults to system time.</param>
    public BrowserSession(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Parameterless constructor required for System.Text.Json deserialization
    // Some serializers require a true parameterless ctor (no parameters at all)
    // — providing this keeps deserialization predictable and preserves the
    // TimeProvider defaulting behavior.
    public BrowserSession() : this(null)
    {
    }

    /// <summary>
    /// Check if this session has expired using the system clock.
    /// </summary>
    public bool IsExpired()
    {
        return _timeProvider.GetUtcNow().UtcDateTime >= ExpiresAt;
    }

    /// <summary>
    /// Check if this session has expired using the specified time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider to use for the expiration check.</param>
    public bool IsExpired(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return timeProvider.GetUtcNow().UtcDateTime >= ExpiresAt;
    }
}

/// <summary>
/// Viewport dimensions for browser session.
/// </summary>
public sealed record ViewportDimensions(int Width, int Height);
