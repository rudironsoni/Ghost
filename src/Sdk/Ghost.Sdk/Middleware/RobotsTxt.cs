namespace Ghost.Sdk.Middleware;

/// <summary>
/// Represents a parsed robots.txt file with rules for different user-agents.
/// </summary>
public class RobotsTxt
{
    private readonly Dictionary<string, UserAgentRules> _rules = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the default crawl delay in seconds.
    /// </summary>
    public double? CrawlDelay { get; set; }

    /// <summary>
    /// Gets the list of sitemaps declared in robots.txt.
    /// </summary>
    public List<string> Sitemaps { get; } = [];

    /// <summary>
    /// Adds rules for a specific user-agent.
    /// </summary>
    /// <param name="userAgent">The user-agent string.</param>
    /// <param name="rules">The rules for this user-agent.</param>
    public void AddRules(string userAgent, UserAgentRules rules)
    {
        ArgumentNullException.ThrowIfNull(userAgent);
        ArgumentNullException.ThrowIfNull(rules);

        _rules[userAgent] = rules;
    }

    /// <summary>
    /// Determines if a path can be fetched by the given user-agent.
    /// </summary>
    /// <param name="path">The path to check (e.g., /about).</param>
    /// <param name="userAgent">The user-agent string.</param>
    /// <returns>True if allowed, false if disallowed.</returns>
    public bool CanFetch(string path, string userAgent)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(userAgent);

        // Find the most specific matching user-agent
        UserAgentRules? rules = GetRulesForUserAgent(userAgent);
        if (rules == null)
            return true; // No rules = allow all

        return rules.IsAllowed(path);
    }

    /// <summary>
    /// Gets the crawl delay for a specific user-agent.
    /// </summary>
    /// <param name="userAgent">The user-agent string.</param>
    /// <returns>The crawl delay in seconds, or null if not specified.</returns>
    public double? GetCrawlDelay(string userAgent)
    {
        ArgumentNullException.ThrowIfNull(userAgent);

        UserAgentRules? rules = GetRulesForUserAgent(userAgent);
        return rules?.CrawlDelay ?? CrawlDelay;
    }

    private UserAgentRules? GetRulesForUserAgent(string userAgent)
    {
        // Exact match
        if (_rules.TryGetValue(userAgent, out UserAgentRules? exactRules))
            return exactRules;

        // Partial match (e.g., user-agent contains "Googlebot")
        foreach ((string? key, UserAgentRules? rules) in _rules)
        {
            if (key != "*" && userAgent.Contains(key, StringComparison.OrdinalIgnoreCase))
                return rules;
        }

        // Wildcard match
        if (_rules.TryGetValue("*", out UserAgentRules? wildcardRules))
            return wildcardRules;

        return null;
    }
}

/// <summary>
/// Represents rules for a specific user-agent in robots.txt.
/// </summary>
public class UserAgentRules
{
    private readonly List<PathRule> _rules = [];

    /// <summary>
    /// Gets or sets the crawl delay in seconds.
    /// </summary>
    public double? CrawlDelay { get; set; }

    /// <summary>
    /// Adds a disallow rule.
    /// </summary>
    /// <param name="path">The path pattern to disallow.</param>
    public void AddDisallow(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!string.IsNullOrWhiteSpace(path))
            _rules.Add(new PathRule(path, false));
    }

    /// <summary>
    /// Adds an allow rule.
    /// </summary>
    /// <param name="path">The path pattern to allow.</param>
    public void AddAllow(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!string.IsNullOrWhiteSpace(path))
            _rules.Add(new PathRule(path, true));
    }

    /// <summary>
    /// Determines if a path is allowed based on the rules.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if allowed, false if disallowed.</returns>
    public bool IsAllowed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (_rules.Count == 0)
            return true;

        // Find the longest matching rule (most specific)
        PathRule? longestMatch = null;
        int longestLength = 0;

        foreach (PathRule rule in _rules)
        {
            if (rule.Matches(path) && rule.Pattern.Length > longestLength)
            {
                longestMatch = rule;
                longestLength = rule.Pattern.Length;
            }
        }

        // If no rule matches, allow by default
        if (longestMatch == null)
            return true;

        return longestMatch.IsAllow;
    }
}

/// <summary>
/// Represents a single path rule (allow or disallow).
/// </summary>
internal sealed class PathRule
{
    public PathRule(string pattern, bool isAllow)
    {
        Pattern = pattern;
        IsAllow = isAllow;
    }

    public string Pattern { get; }
    public bool IsAllow { get; }

    /// <summary>
    /// Checks if a path matches this rule.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path matches this rule.</returns>
    public bool Matches(string path)
    {
        // Handle wildcard patterns
        if (Pattern.Contains('*'))
        {
            string regex = "^" + System.Text.RegularExpressions.Regex.Escape(Pattern).Replace("\\*", ".*") + ".*";
            return System.Text.RegularExpressions.Regex.IsMatch(path, regex);
        }

        // Simple prefix match
        return path.StartsWith(Pattern, StringComparison.OrdinalIgnoreCase);
    }
}
