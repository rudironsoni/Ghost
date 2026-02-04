using System.Text.RegularExpressions;

namespace Ghost.Sdk.Spider.Core.Extraction.Selectors;

/// <summary>
/// Selects values from content using regular expressions.
/// </summary>
public class RegexSelector : ISelector
{
    private readonly Regex _regex;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegexSelector"/> class.
    /// </summary>
    /// <param name="pattern">The regular expression pattern.</param>
    /// <param name="group">The capture group index to extract (0 = whole match).</param>
    /// <param name="options">Regular expression options.</param>
    public RegexSelector(string pattern, int group = 0, RegexOptions options = RegexOptions.None)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentNullException(nameof(pattern));

        Expression = pattern;
        Group = group;
        _regex = new Regex(pattern, options);
    }

    /// <inheritdoc/>
    public string Expression { get; }

    /// <summary>
    /// Gets the capture group index to extract.
    /// </summary>
    public int Group { get; }

    /// <inheritdoc/>
    public List<string> Select(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new List<string>();

        var matches = _regex.Matches(content);
        var results = new List<string>();

        foreach (Match match in matches)
        {
            if (match.Success && Group < match.Groups.Count)
            {
                var value = match.Groups[Group].Value;
                if (!string.IsNullOrEmpty(value))
                    results.Add(value);
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public string? SelectFirst(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var match = _regex.Match(content);
        if (!match.Success || Group >= match.Groups.Count)
            return null;

        var value = match.Groups[Group].Value;
        return !string.IsNullOrEmpty(value) ? value : null;
    }

    /// <inheritdoc/>
    public bool Validate()
    {
        try
        {
            _regex.Match(string.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
