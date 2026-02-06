using System.Text.RegularExpressions;

namespace Ghost.Sdk.Spider.Core.Entities.Formatters;

/// <summary>
/// Replaces occurrences of a string with another string, supporting both literal and regex patterns.
/// </summary>
/// <remarks>
/// This formatter performs string replacement with optional case-insensitive matching.
/// It can replace all occurrences or just the first one.
/// When OldValue contains a regex pattern (e.g., "\d+"), it will be used as a regex.
/// </remarks>
/// <example>
/// <code>
/// var formatter = new ReplaceFormatter { OldValue = "foo", NewValue = "bar" };
/// var result = formatter.Format("foo is foo"); // Returns "bar is bar"
/// 
/// var regexFormatter = new ReplaceFormatter { OldValue = "\\d+", NewValue = "X" };
/// var result2 = regexFormatter.Format("Item 123"); // Returns "Item X"
/// </code>
/// </example>
public class ReplaceFormatter : Formatter
{
    /// <summary>
    /// Gets or sets the string to be replaced (supports regex patterns).
    /// </summary>
    /// <value>The search string or regex pattern.</value>
    public required string OldValue { get; set; }

    /// <summary>
    /// Gets or sets the replacement string.
    /// </summary>
    /// <value>The replacement string.</value>
    public required string NewValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the replacement is case-insensitive.
    /// </summary>
    /// <value><c>true</c> for case-insensitive replacement; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    public bool IgnoreCase { get; set; } = false;

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value is not string str)
            return value;

        if (string.IsNullOrEmpty(str))
            return str;

        // Try to use as regex pattern
        try
        {
            var options = IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            return Regex.Replace(str, OldValue, NewValue, options);
        }
        catch (ArgumentException)
        {
            // If it's not a valid regex, fall back to simple string replacement
            var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return str.Replace(OldValue, NewValue, comparison);
        }
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (string.IsNullOrEmpty(OldValue))
            throw new InvalidOperationException("OldValue cannot be null or empty.");

        if (NewValue == null)
            throw new InvalidOperationException("NewValue cannot be null.");
    }
}
