using System;
using System.Collections.Generic;
using System.Text;

namespace Ghost.Plugin.LinkedIn.Internal;

/// <summary>
/// Builds LinkedIn guest job search URLs with boolean query support.
/// </summary>
internal static class LinkedInQueryBuilder
{
    private const string BaseUrl = "https://www.linkedin.com";
    private const string SearchPath = "/jobs-guest/jobs/api/seeMoreJobPostings/search";

    /// <summary>
    /// Builds a LinkedIn guest job search URL for the provided query.
    /// </summary>
    /// <param name="query">Search expression supporting AND, OR, NOT, quotes, and parentheses.</param>
    /// <param name="location">Location filter value.</param>
    /// <param name="offset">Pagination offset (start index).</param>
    /// <param name="postedWithin">Optional recency filter (converted to f_TPR seconds).</param>
    /// <returns>Absolute LinkedIn guest search URL.</returns>
    public static string BuildSearchUrl(string query, string location, int offset = 0, TimeSpan? postedWithin = null)
    {
        string normalizedQuery = NormalizeQuery(query ?? string.Empty);
        string encodedQuery = Uri.EscapeDataString(normalizedQuery);
        string encodedLocation = Uri.EscapeDataString(location ?? string.Empty);
        int start = Math.Max(0, offset);

        var parameters = new List<string>(4)
        {
            $"keywords={encodedQuery}",
            $"location={encodedLocation}",
            $"start={start}"
        };

        string? tpr = BuildPostedWithinFilter(postedWithin);
        if (!string.IsNullOrEmpty(tpr))
        {
            parameters.Add($"f_TPR={tpr}");
        }

        return $"{BaseUrl}{SearchPath}?{string.Join("&", parameters)}";
    }

    private static string NormalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return string.Empty;

        List<string> tokens = Tokenize(query);
        if (tokens.Count == 0) return string.Empty;

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            if (IsQuotedToken(token)) continue;

            if (IsBooleanOperator(token))
            {
                tokens[i] = token.ToUpperInvariant();
            }
        }

        return JoinTokens(tokens);
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        foreach (char ch in input)
        {
            if (ch == '"')
            {
                if (inQuotes)
                {
                    current.Append(ch);
                    tokens.Add(current.ToString());
                    current.Clear();
                    inQuotes = false;
                }
                else
                {
                    if (current.Length > 0)
                    {
                        current.Append(ch);
                    }
                    else
                    {
                        Flush();
                        current.Append(ch);
                        inQuotes = true;
                    }
                }
                continue;
            }

            if (inQuotes)
            {
                current.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                Flush();
                continue;
            }

            if (ch is '(' or ')')
            {
                Flush();
                tokens.Add(ch.ToString());
                continue;
            }

            current.Append(ch);
        }

        Flush();

        return tokens;
    }

    private static string JoinTokens(List<string> tokens)
    {
        if (tokens.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        string? previous = null;

        foreach (string token in tokens)
        {
            if (builder.Length == 0)
            {
                builder.Append(token);
                previous = token;
                continue;
            }

            if (token == ")")
            {
                if (builder.Length > 0 && builder[^1] == ' ')
                {
                    builder.Length--;
                }
                builder.Append(token);
                previous = token;
                continue;
            }

            if (previous == "(")
            {
                builder.Append(token);
                previous = token;
                continue;
            }

            if (token == "(")
            {
                builder.Append(' ').Append(token);
                previous = token;
                continue;
            }

            builder.Append(' ').Append(token);
            previous = token;
        }

        return builder.ToString();
    }

    private static bool IsQuotedToken(string token)
    {
        return token.StartsWith('"');
    }

    private static bool IsBooleanOperator(string token)
    {
        return token.Equals("AND", StringComparison.OrdinalIgnoreCase)
            || token.Equals("OR", StringComparison.OrdinalIgnoreCase)
            || token.Equals("NOT", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildPostedWithinFilter(TimeSpan? postedWithin)
    {
        if (!postedWithin.HasValue) return null;

        TimeSpan span = postedWithin.Value;
        if (span <= TimeSpan.Zero) return null;

        long seconds = (long)Math.Round(span.TotalSeconds);
        if (seconds <= 0) return null;

        if (seconds > int.MaxValue) seconds = int.MaxValue;

        return $"r{seconds}";
    }
}
