using System.Text.RegularExpressions;

namespace Ghost.Sdk.Spider.Core.Entities.Formatters;

/// <summary>
/// Applies regular expression transformation to string values.
/// </summary>
/// <remarks>
/// This formatter can either replace matches or extract specific groups from the input.
/// It supports standard .NET regex options including case-insensitive matching.
/// </remarks>
/// <example>
/// <code>
/// // Extract phone number
/// var extractor = new RegexFormatter
/// {
///     Pattern = @"\d{3}-\d{3}-\d{4}",
///     Group = 0
/// };
/// var phone = extractor.Format("Call us at 555-123-4567"); // Returns "555-123-4567"
///
/// // Replace using capture groups
/// var replacer = new RegexFormatter
/// {
///     Pattern = @"(\d{3})-(\d{3})-(\d{4})",
///     Replacement = "($1) $2-$3"
/// };
/// var formatted = replacer.Format("555-123-4567"); // Returns "(555) 123-4567"
/// </code>
/// </example>
public class RegexFormatter : Formatter
{
    private Regex? _compiledRegex;

    /// <summary>
    /// Gets or sets the regular expression pattern.
    /// </summary>
    /// <value>The regex pattern string.</value>
    public required string Pattern { get; set; }

    /// <summary>
    /// Gets or sets the replacement string. If null, extracts the first match.
    /// </summary>
    /// <value>
    /// The replacement pattern supporting capture groups like $1, $2, etc.
    /// If null, the formatter extracts matches instead of replacing.
    /// </value>
    public string? Replacement { get; set; }

    /// <summary>
    /// Gets or sets the capture group index to extract (0 = whole match).
    /// Only used when <see cref="Replacement"/> is null.
    /// </summary>
    /// <value>The zero-based group index. Defaults to 0.</value>
    public int Group { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the pattern is case-insensitive.
    /// </summary>
    /// <value><c>true</c> for case-insensitive matching; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    public bool IgnoreCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use multiline mode.
    /// </summary>
    /// <value><c>true</c> to enable multiline mode; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    public bool Multiline { get; set; }

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value is not string str)
            return value;

        if (string.IsNullOrEmpty(str))
            return str;

        // Lazy compile regex
        _compiledRegex ??= CompileRegex();

        if (Replacement != null)
        {
            return _compiledRegex.Replace(str, Replacement);
        }

        Match match = _compiledRegex.Match(str);
        if (!match.Success)
            return str;

        if (Group >= match.Groups.Count)
            return str;

        return match.Groups[Group].Value;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (string.IsNullOrWhiteSpace(Pattern))
            throw new InvalidOperationException("Pattern cannot be null or whitespace.");

        try
        {
            _ = CompileRegex();
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Invalid regex pattern: {Pattern}", ex);
        }

        if (Group < 0)
            throw new InvalidOperationException("Group index cannot be negative.");
    }

    private Regex CompileRegex()
    {
        RegexOptions options = RegexOptions.Compiled;

        if (IgnoreCase)
            options |= RegexOptions.IgnoreCase;

        if (Multiline)
            options |= RegexOptions.Multiline;

        return new Regex(Pattern, options, TimeSpan.FromSeconds(5));
    }
}
