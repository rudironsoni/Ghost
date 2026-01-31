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

