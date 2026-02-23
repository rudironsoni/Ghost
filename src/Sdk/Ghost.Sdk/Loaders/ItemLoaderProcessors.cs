using System.Linq;
using System.Text.RegularExpressions;

namespace Ghost.Sdk.Loaders;

/// <summary>
/// Provides built-in processor functions for transforming extracted data.
/// </summary>
public static partial class ItemLoaderProcessors
{
    /// <summary>
    /// Returns a processor that trims whitespace from the input.
    /// </summary>
    /// <returns>A processor function that strips leading and trailing whitespace.</returns>
    public static Func<string, string> Strip() => s => s?.Trim() ?? string.Empty;

    /// <summary>
    /// Returns a processor that joins values using a custom separator.
    /// </summary>
    /// <param name="separator">The separator to use for joining.</param>
    /// <returns>A processor function that replaces commas with the specified separator.</returns>
    public static Func<string, string> Join(string separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        return s =>
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }

            // Split by comma, trim each value, filter empty ones, then join with separator
            IEnumerable<string> values = s.Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v));
            return string.Join(separator, values);
        };
    }

    /// <summary>
    /// Returns a processor that takes the first N characters of the input.
    /// </summary>
    /// <param name="count">The number of characters to take.</param>
    /// <returns>A processor function that truncates the input.</returns>
    public static Func<string, string> Take(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
        }

        return s => s?.Length > count ? s[..count] : s ?? string.Empty;
    }

    /// <summary>
    /// Returns a processor that replaces all occurrences of a substring.
    /// </summary>
    /// <param name="oldValue">The string to replace.</param>
    /// <param name="newValue">The string to replace with.</param>
    /// <returns>A processor function that performs the replacement.</returns>
    public static Func<string, string> Replace(string oldValue, string newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        ArgumentNullException.ThrowIfNull(newValue);

        return s => s?.Replace(oldValue, newValue) ?? string.Empty;
    }

    /// <summary>
    /// Returns a processor that converts the input to lowercase.
    /// </summary>
    /// <returns>A processor function that converts to lowercase.</returns>
    public static Func<string, string> ToLower() => s => s?.ToLowerInvariant() ?? string.Empty;

    /// <summary>
    /// Returns a processor that converts the input to uppercase.
    /// </summary>
    /// <returns>A processor function that converts to uppercase.</returns>
    public static Func<string, string> ToUpper() => s => s?.ToUpperInvariant() ?? string.Empty;

    /// <summary>
    /// Returns a processor that extracts the first match of a regular expression.
    /// </summary>
    /// <param name="pattern">The regular expression pattern.</param>
    /// <returns>A processor function that extracts using regex.</returns>
    public static Func<string, string> RegexExtract(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        return s =>
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            Match match = Regex.Match(s, pattern);
            return match.Success ? match.Value : string.Empty;
        };
    }

    /// <summary>
    /// Returns a processor that removes HTML tags from the input.
    /// </summary>
    /// <returns>A processor function that strips HTML tags.</returns>
    public static Func<string, string> StripHtml() => s =>
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        return HtmlTagRegex().Replace(s, string.Empty);
    };

    /// <summary>
    /// Returns a processor that normalizes whitespace (multiple spaces to single space).
    /// </summary>
    /// <returns>A processor function that normalizes whitespace.</returns>
    public static Func<string, string> NormalizeWhitespace() => s =>
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(s.Trim(), " ");
    };

    /// <summary>
    /// Returns a processor that provides a default value if the input is null or empty.
    /// </summary>
    /// <param name="defaultValue">The default value to use.</param>
    /// <returns>A processor function that provides a default value.</returns>
    public static Func<string, string> DefaultIfEmpty(string defaultValue)
    {
        ArgumentNullException.ThrowIfNull(defaultValue);

        return s => string.IsNullOrWhiteSpace(s) ? defaultValue : s;
    }

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
