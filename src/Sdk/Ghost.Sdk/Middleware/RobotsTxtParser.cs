namespace Ghost.Sdk.Middleware;

/// <summary>
/// Parser for robots.txt files conforming to the Robots Exclusion Protocol.
/// </summary>
public static class RobotsTxtParser
{
    /// <summary>
    /// Parses a robots.txt file content.
    /// </summary>
    /// <param name="content">The raw content of the robots.txt file.</param>
    /// <returns>A parsed RobotsTxt object.</returns>
    public static RobotsTxt Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var robotsTxt = new RobotsTxt();
        string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        UserAgentRules? currentRules = null;
        List<string> currentUserAgents = [];

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            // Split on first colon
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
                continue;

            string directive = line[..colonIndex].Trim();
            string value = line[(colonIndex + 1)..].Trim();

            // Remove inline comments
            int commentIndex = value.IndexOf('#');
            if (commentIndex >= 0)
                value = value[..commentIndex].Trim();

            if (string.IsNullOrWhiteSpace(value))
                continue;

            switch (directive.ToLowerInvariant())
            {
                case "user-agent":
                    // Save previous user-agent rules
                    if (currentRules != null && currentUserAgents.Count > 0)
                    {
                        foreach (string ua in currentUserAgents)
                        {
                            robotsTxt.AddRules(ua, currentRules);
                        }
                    }

                    // Start new user-agent block
                    currentUserAgents.Clear();
                    currentUserAgents.Add(value);
                    currentRules = new UserAgentRules();
                    break;

                case "disallow":
                    currentRules?.AddDisallow(value);
                    break;

                case "allow":
                    currentRules?.AddAllow(value);
                    break;

                case "crawl-delay":
                    if (double.TryParse(value, out double delay))
                    {
                        if (currentRules != null)
                        {
                            currentRules.CrawlDelay = delay;
                        }
                        else
                        {
                            robotsTxt.CrawlDelay = delay;
                        }
                    }
                    break;

                case "sitemap":
                    robotsTxt.Sitemaps.Add(value);
                    break;

                default:
                    // Ignore unknown directives
                    break;
            }
        }

        // Save final user-agent rules
        if (currentRules != null && currentUserAgents.Count > 0)
        {
            foreach (string ua in currentUserAgents)
            {
                robotsTxt.AddRules(ua, currentRules);
            }
        }

        return robotsTxt;
    }
}
