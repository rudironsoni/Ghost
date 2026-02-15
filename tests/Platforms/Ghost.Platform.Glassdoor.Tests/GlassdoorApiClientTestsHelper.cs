using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ghost.Plugin.Glassdoor.Tests;

/// <summary>
/// Helper class for Glassdoor API client tests to expose internal extraction logic.
/// </summary>
public static class GlassdoorApiClientTestsHelper
{
    /// <summary>
    /// Extracts CSRF token from HTML using the same patterns as GlassdoorApiClient.
    /// </summary>
    public static string? ExtractCsrfTokenFromHtml(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        // Multiple CSRF token extraction patterns
        var patterns = new[]
        {
            "token\"\\s*:\\s*\"([^\"]+)\"",
            "<meta[^>]*csrf-token[^>]*content=\"([^\"]+)\"[^>]*>",
            "window\\.\\w+\\s*=\\s*\\{\\s*\"token\"\\s*:\\s*\"([^\"]+)\"",
            "\"gd-csrf-token\"\\s*:\\s*\"([^\"]+)\"",
            "data-csrf-token=\"([^\"]+)\""
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var token = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(token) && token.Length > 10)
                {
                    return token;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if HTML content indicates a consent or blocked page.
    /// </summary>
    public static bool IsConsentOrBlockedPage(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return true;

        return html.Contains("consent", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("robot check", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("human verification", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("security check", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("terms of service", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("privacy policy", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("cookie policy", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("before you continue", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("accept cookies", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("manage cookies", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses GraphQL error response to determine if errors exist and if retry is recommended.
    /// </summary>
    public static (bool hasErrors, bool shouldRetry) ParseGraphQLErrors(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (true, false);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.ValueKind == JsonValueKind.Object)
                    {
                        var message = error.TryGetProperty("message", out var msg)
                            ? msg.GetString() ?? ""
                            : "";
                        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("temporary", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, true);
                        }
                        if (message.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, false);
                        }
                    }
                }
                return (true, true);
            }
            return (false, false);
        }
        catch
        {
            return (true, false);
        }
    }
}
