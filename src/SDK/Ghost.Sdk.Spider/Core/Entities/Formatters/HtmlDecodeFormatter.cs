using System.Net;

namespace Ghost.Sdk.Spider.Core.Entities.Formatters;

/// <summary>
/// Decodes HTML-encoded strings.
/// </summary>
/// <remarks>
/// This formatter converts HTML entity references back to their character equivalents.
/// For example, "&amp;lt;" becomes "&lt;", "&amp;amp;" becomes "&amp;", etc.
/// It uses <see cref="WebUtility.HtmlDecode(string?)"/> for decoding.
/// </remarks>
/// <example>
/// <code>
/// var formatter = new HtmlDecodeFormatter();
/// var decoded = formatter.Format("&amp;lt;div&amp;gt;Hello &amp;amp; goodbye&amp;lt;/div&amp;gt;");
/// // Returns: "&lt;div&gt;Hello &amp; goodbye&lt;/div&gt;"
/// 
/// var decoded2 = formatter.Format("Price: &amp;pound;99.99");
/// // Returns: "Price: £99.99"
/// </code>
/// </example>
public class HtmlDecodeFormatter : Formatter
{
    /// <summary>
    /// Gets or sets a value indicating whether to decode multiple times.
    /// </summary>
    /// <value>
    /// <c>true</c> to decode until no more entities remain (useful for double-encoded content);
    /// otherwise, <c>false</c>. Defaults to <c>false</c>.
    /// </value>
    /// <remarks>
    /// Use with caution: enabling this can cause issues if the content legitimately
    /// contains strings that look like HTML entities.
    /// </remarks>
    public bool DecodeMultipleTimes { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of decode iterations when <see cref="DecodeMultipleTimes"/> is true.
    /// </summary>
    /// <value>The maximum iterations. Defaults to 5 to prevent infinite loops.</value>
    public int MaxDecodeIterations { get; set; } = 5;

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value is not string str)
            return value;

        if (string.IsNullOrEmpty(str))
            return str;

        if (!str.Contains('&'))
            return str; // Quick optimization - no entities to decode

        if (!DecodeMultipleTimes)
        {
            return WebUtility.HtmlDecode(str);
        }

        // Decode multiple times until no more changes or max iterations reached
        var decoded = str;
        var iterations = 0;

        while (iterations < MaxDecodeIterations)
        {
            var nextDecode = WebUtility.HtmlDecode(decoded);
            if (nextDecode == decoded) // No more changes
                break;

            decoded = nextDecode;
            iterations++;
        }

        return decoded;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (MaxDecodeIterations < 1)
            throw new InvalidOperationException("MaxDecodeIterations must be at least 1.");
    }
}
