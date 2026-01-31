using System.Text.RegularExpressions;

namespace Ghost.Platform.Glassdoor.Tests;

public static class GlassdoorApiClientTestsHelper
{
    public static string? ExtractCsrfTokenFromHtml(string html)
    {
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
}
