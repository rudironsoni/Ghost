using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.LinkedIn.Internal;

internal sealed class LinkedInRateLimitException : Exception
{
    public LinkedInRateLimitException() { }
    public LinkedInRateLimitException(string message) : base(message) { }
    public LinkedInRateLimitException(string message, Exception inner) : base(message, inner) { }
}

internal static class LinkedInRateLimitDetector
{
    private static readonly Action<ILogger, Exception?> s_logNoRateLimit =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(CheckAsync)), "No rate limit indicators found.");

    public static async Task CheckAsync(Ghost.IPage page, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        if (page == null) return;

        try
        {
            string url = page.Url ?? string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                if (url.Contains("/check/challenge", StringComparison.OrdinalIgnoreCase) || url.Contains("/checkpoint/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new LinkedInRateLimitException($"LinkedIn rate limit / checkpoint detected via URL: {url}");
                }
            }

            // Try to get full content; fall back gracefully if not available
            string html = string.Empty;
            try
            {
                html = await page.GetContentAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get page content: {ex}");
            }

            if (!string.IsNullOrEmpty(html))
            {
                string lower = html.ToLowerInvariant();
                if (lower.Contains("security check") || lower.Contains("too many requests"))
                {
                    throw new LinkedInRateLimitException("LinkedIn rate limit or security check detected in page content.");
                }
                else
                {
                    try { if (logger is not null) s_logNoRateLimit(logger, null); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to log: {ex}"); }
                }
            }
        }
        catch (LinkedInRateLimitException)
        {
            throw;
        }
        catch (Exception)
        {
            // Non-fatal: detection should not throw other exceptions
        }
    }
}
