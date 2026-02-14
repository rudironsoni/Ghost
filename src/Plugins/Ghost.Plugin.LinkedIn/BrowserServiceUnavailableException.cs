using System;

namespace Ghost.Plugin.LinkedIn;

/// <summary>
/// Exception thrown when the browser automation service is unavailable.
/// This can occur due to Playwright initialization failures, proxy connection issues,
/// or browser process failures.
/// </summary>
public sealed class BrowserServiceUnavailableException : Exception
{
    public BrowserServiceUnavailableException()
        : base("Browser automation service is currently unavailable")
    {
    }

    public BrowserServiceUnavailableException(string message)
        : base(message)
    {
    }

    public BrowserServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
