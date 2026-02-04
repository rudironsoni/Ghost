namespace Ghost.Sdk.Spider.Core.Entities.Formatters;

/// <summary>
/// Trims whitespace or specified characters from string values.
/// </summary>
/// <remarks>
/// This formatter can trim from the start, end, or both sides of a string.
/// It supports custom trim characters or defaults to whitespace trimming.
/// </remarks>
/// <example>
/// <code>
/// var formatter = new TrimFormatter();
/// var result = formatter.Format("  hello  "); // Returns "hello"
/// 
/// var customFormatter = new TrimFormatter { TrimChars = ",.;", TrimStart = true };
/// var result2 = customFormatter.Format(",,,hello..."); // Returns "hello..."
/// </code>
/// </example>
public class TrimFormatter : Formatter
{
    /// <summary>
    /// Gets or sets the characters to trim. If null, trims whitespace.
    /// </summary>
    /// <value>A string containing characters to trim, or null for whitespace.</value>
    public string? TrimChars { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to trim only the start of the string.
    /// </summary>
    /// <value><c>true</c> to trim only the start; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    public bool TrimStart { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to trim only the end of the string.
    /// </summary>
    /// <value><c>true</c> to trim only the end; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    public bool TrimEnd { get; set; } = false;

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value is not string str)
            return value;

        if (string.IsNullOrEmpty(str))
            return str;

        var trimChars = TrimChars?.ToCharArray();

        if (TrimStart && !TrimEnd)
            return trimChars != null ? str.TrimStart(trimChars) : str.TrimStart();

        if (TrimEnd && !TrimStart)
            return trimChars != null ? str.TrimEnd(trimChars) : str.TrimEnd();

        return trimChars != null ? str.Trim(trimChars) : str.Trim();
    }
}
