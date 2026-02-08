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

    /// <summary>
    /// Check if this session has expired.
    /// </summary>
    public bool IsExpired()
    {
        return DateTime.UtcNow >= ExpiresAt;
    }
}

/// <summary>
/// Viewport dimensions for browser session.
/// </summary>
public sealed record ViewportDimensions(int Width, int Height);
