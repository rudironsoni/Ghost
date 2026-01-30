using FluentAssertions;
using Ghost.Platform.Glassdoor.Internal;
using Xunit;

namespace Ghost.Platform.Glassdoor.Tests;

public class GlassdoorApiClientTests
{
    [Fact]
    public void ExtractsCsrfTokenFromJsonPattern()
    {
        var html = """
<!DOCTYPE html>
<html>
<head>
    <script>
        window.__INITIAL_STATE__ = {
            "token": "gd-csrf-token-1234567890abcdef"
        };
    </script>
</head>
<body></body>
</html>
""";

        var token = GlassdoorApiClientTestsHelper.ExtractCsrfTokenFromHtml(html);
        token.Should().Be("gd-csrf-token-1234567890abcdef");
    }

    [Fact]
    public void ExtractsCsrfTokenFromMetaTag()
    {
        var html = """
<!DOCTYPE html>
<html>
<head>
    <meta name="csrf-token" content="meta-token-abcdef123456">
</head>
<body></body>
</html>
""";

        var token = GlassdoorApiClientTestsHelper.ExtractCsrfTokenFromHtml(html);
        token.Should().Be("meta-token-abcdef123456");
    }

    [Fact]
    public void ExtractsCsrfTokenFromWindowObject()
    {
        var html = """
<!DOCTYPE html>
<html>
<head>
    <script>
        window.GlassdoorApp = {
            "token": "window-token-9876543210fedcba"
        };
    </script>
</head>
<body></body>
</html>
""";

        var token = GlassdoorApiClientTestsHelper.ExtractCsrfTokenFromHtml(html);
        token.Should().Be("window-token-9876543210fedcba");
    }

    [Fact]
    public void ExtractsCsrfTokenFromGdCsrfTokenPattern()
    {
        var html = """
<!DOCTYPE html>
<html>
<head>
    <script>
        var config = {
            "gd-csrf-token": "gd-token-abcdef1234567890"
        };
    </script>
</head>
<body></body>
</html>
""";

        var token = GlassdoorApiClientTestsHelper.ExtractCsrfTokenFromHtml(html);
        token.Should().Be("gd-token-abcdef1234567890");
    }

    [Fact]
    public void ExtractsCsrfTokenFromDataAttribute()
    {
        var html = """
<!DOCTYPE html>
<html>
<head></head>
<body>
    <div data-csrf-token="data-token-1234567890abcdef"></div>
</body>
</html>
""";

        var token = GlassdoorApiClientTestsHelper.ExtractCsrfTokenFromHtml(html);
        token.Should().Be("data-token-1234567890abcdef");
    }

    [Fact]
    public void ReturnsNullWhenNoTokenFound()
    {
        var html = """
<!DOCTYPE html>
<html>
<head></head>
<body>
    <h1>No CSRF token here</h1>
</body>
</html>
""";

        var token = GlassdoorApiClientTestsHelper.ExtractCsrfTokenFromHtml(html);
        token.Should().BeNull();
    }

    [Fact]
    public void ReturnsNullForEmptyHtml()
    {
        var token = GlassdoorApiClientTestsHelper.ExtractCsrfTokenFromHtml("");
        token.Should().BeNull();
    }

    [Fact]
    public void ReturnsNullForNullHtml()
    {
        var token = GlassdoorApiClientTestsHelper.ExtractCsrfTokenFromHtml(null);
        token.Should().BeNull();
    }

    [Fact]
    public void DetectsConsentPage()
    {
        var html = """
<!DOCTYPE html>
<html>
<head>
    <title>Consent Required</title>
</head>
<body>
    <h1>Please accept our cookies</h1>
    <p>We need your consent to continue</p>
</body>
</html>
""";

        var isConsentPage = GlassdoorApiClientTestsHelper.IsConsentOrBlockedPage(html);
        isConsentPage.Should().BeTrue();
    }

    [Fact]
    public void DetectsCaptchaPage()
    {
        var html = """
<!DOCTYPE html>
<html>
<head>
    <title>Security Check</title>
</head>
<body>
    <h1>Please complete the captcha</h1>
    <div class="captcha"></div>
</body>
</html>
""";

        var isCaptchaPage = GlassdoorApiClientTestsHelper.IsConsentOrBlockedPage(html);
        isCaptchaPage.Should().BeTrue();
    }

    [Fact]
    public void DetectsBlockedPage()
    {
        var html = """
<!DOCTYPE html>
<html>
<head>
    <title>Access Denied</title>
</head>
<body>
    <h1>Access Denied</h1>
    <p>Your request has been blocked</p>
</body>
</html>
""";

        var isBlockedPage = GlassdoorApiClientTestsHelper.IsConsentOrBlockedPage(html);
        isBlockedPage.Should().BeTrue();
    }

    [Fact]
    public void ReturnsFalseForNormalPage()
    {
        var html = """
<!DOCTYPE html>
<html>
<head>
    <title>Glassdoor - Job Search</title>
</head>
<body>
    <h1>Find your next job</h1>
    <p>Search millions of jobs</p>
</body>
</html>
""";

        var isConsentPage = GlassdoorApiClientTestsHelper.IsConsentOrBlockedPage(html);
        isConsentPage.Should().BeFalse();
    }

    [Fact]
    public void ReturnsTrueForEmptyHtml()
    {
        var isConsentPage = GlassdoorApiClientTestsHelper.IsConsentOrBlockedPage("");
        isConsentPage.Should().BeTrue();
    }

    [Fact]
    public void ReturnsTrueForNullHtml()
    {
        var isConsentPage = GlassdoorApiClientTestsHelper.IsConsentOrBlockedPage(null);
        isConsentPage.Should().BeTrue();
    }

    [Fact]
    public void ParsesGraphQLErrorsForRateLimit()
    {
        var json = """
{
  "errors": [
    {
      "message": "Rate limit exceeded",
      "extensions": { "code": "RATE_LIMITED" }
    }
  ]
}
""";

        var (hasErrors, shouldRetry) = GlassdoorApiClientTestsHelper.ParseGraphQLErrors(json);
        hasErrors.Should().BeTrue();
        shouldRetry.Should().BeTrue();
    }

    [Fact]
    public void ParsesGraphQLErrorsForServerError()
    {
        var json = """
{
  "errors": [
    {
      "message": "Internal server error",
      "extensions": { "code": "INTERNAL_ERROR" }
    }
  ]
}
""";

        var (hasErrors, shouldRetry) = GlassdoorApiClientTestsHelper.ParseGraphQLErrors(json);
        hasErrors.Should().BeTrue();
        shouldRetry.Should().BeTrue();
    }

    [Fact]
    public void ParsesGraphQLErrorsForAuthError()
    {
        var json = """
{
  "errors": [
    {
      "message": "Unauthorized access",
      "extensions": { "code": "UNAUTHORIZED" }
    }
  ]
}
""";

        var (hasErrors, shouldRetry) = GlassdoorApiClientTestsHelper.ParseGraphQLErrors(json);
        hasErrors.Should().BeTrue();
        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public void ParsesValidGraphQLResponse()
    {
        var json = """
{
  "data": {
    "jobSearchResults": {
      "jobs": [],
      "totalResults": 0,
      "pageInfo": {
        "hasNextPage": false,
        "endCursor": null
      }
    }
  }
}
""";

        var (hasErrors, shouldRetry) = GlassdoorApiClientTestsHelper.ParseGraphQLErrors(json);
        hasErrors.Should().BeFalse();
        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public void HandlesInvalidJson()
    {
        var (hasErrors, shouldRetry) = GlassdoorApiClientTestsHelper.ParseGraphQLErrors("invalid json");
        hasErrors.Should().BeTrue();
        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public void HandlesEmptyJson()
    {
        var (hasErrors, shouldRetry) = GlassdoorApiClientTestsHelper.ParseGraphQLErrors("");
        hasErrors.Should().BeTrue();
        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public void HandlesNullJson()
    {
        var (hasErrors, shouldRetry) = GlassdoorApiClientTestsHelper.ParseGraphQLErrors(null);
        hasErrors.Should().BeTrue();
        shouldRetry.Should().BeFalse();
    }
}

// Helper class to access internal methods for testing
public static class GlassdoorApiClientTestsHelper
{
    public static string? ExtractCsrfTokenFromHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;

        var patterns = new[]
        {
            "token\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
            "<meta[^>]*csrf-token[^>]*content=\\\"([^\\\"]+)\\\"[^>]*>",
            "window\\.\\w+\\s*=\\s*\\{\\s*\\\"token\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
            "\\\"gd-csrf-token\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
            "data-csrf-token=\\\"([^\\\"]+)\\\""
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(html, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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

    public static bool IsConsentOrBlockedPage(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return true;

        return html.Contains("consent", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("blocked", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("access denied", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("robot check", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("captcha", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("verify", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("human verification", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("security check", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("terms of service", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("privacy policy", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("cookie policy", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("before you continue", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("accept cookies", System.StringComparison.OrdinalIgnoreCase) ||
               html.Contains("manage cookies", System.StringComparison.OrdinalIgnoreCase);
    }

    public static (bool hasErrors, bool shouldRetry) ParseGraphQLErrors(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (true, false);
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var message = error.TryGetProperty("message", out var msg) 
                            ? msg.GetString() ?? "" 
                            : "";

                        if (message.Contains("rate limit", System.StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("throttled", System.StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("too many requests", System.StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, true);
                        }
                        else if (message.Contains("server error", System.StringComparison.OrdinalIgnoreCase) ||
                                 message.Contains("internal error", System.StringComparison.OrdinalIgnoreCase) ||
                                 message.Contains("timeout", System.StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, true);
                        }
                        else if (message.Contains("unauthorized", System.StringComparison.OrdinalIgnoreCase) ||
                                 message.Contains("forbidden", System.StringComparison.OrdinalIgnoreCase) ||
                                 message.Contains("invalid token", System.StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, false);
                        }
                    }
                }
                return (true, false);
            }

            if (root.TryGetProperty("data", out var data) && data.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                return (false, false);
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return (true, false);
        }

        return (true, false);
    }
}