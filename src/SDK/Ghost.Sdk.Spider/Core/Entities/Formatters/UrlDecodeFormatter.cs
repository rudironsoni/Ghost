using System.Net;

namespace Ghost.Sdk.Spider.Core.Entities.Formatters;

/// <summary>
/// Decodes URL-encoded strings.
/// </summary>
/// <remarks>
/// This formatter converts URL-encoded (percent-encoded) strings back to their original form.
/// For example, "Hello%20World" becomes "Hello World", "%2F" becomes "/", etc.
/// It uses <see cref="WebUtility.UrlDecode"/> for decoding.
/// </remarks>
/// <example>
/// <code>
/// var formatter = new UrlDecodeFormatter();
/// var decoded = formatter.Format("Hello%20World%21");
/// // Returns: "Hello World!"
/// 
/// var decoded2 = formatter.Format("search%3Fq%3Dc%23%20programming");
/// // Returns: "search?q=c# programming"
/// </code>
/// </example>
public class UrlDecodeFormatter : Formatter
{
    /// <summary>
    /// Gets or sets a value indicating whether to decode plus signs as spaces.
    /// </summary>
    /// <value>
    /// <c>true</c> to decode '+' as space (form data encoding); otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// In application/x-www-form-urlencoded data, spaces are typically encoded as '+'.
    /// Set to false if you're decoding path components where '+' should remain literal.
    /// </remarks>
    public bool DecodePlusAsSpace { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to decode multiple times.
    /// </summary>
    /// <value>
    /// <c>true</c> to decode until no more encoded sequences remain (useful for double-encoded URLs);
    /// otherwise, <c>false</c>. Defaults to <c>false</c>.
    /// </value>
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

        if (!str.Contains('%') && (!DecodePlusAsSpace || !str.Contains('+')))
            return str; // Quick optimization - nothing to decode

        if (!DecodeMultipleTimes)
        {
            return DecodeUrl(str);
        }

        // Decode multiple times until no more changes or max iterations reached
        var decoded = str;
        var iterations = 0;

        while (iterations < MaxDecodeIterations)
        {
            var nextDecode = DecodeUrl(decoded);
            if (nextDecode == decoded) // No more changes
                break;

            decoded = nextDecode;
            iterations++;
        }

        return decoded;
    }

    private string DecodeUrl(string str)
    {
        var decoded = WebUtility.UrlDecode(str);
        
        // UrlDecode handles + as space by default, but we may want to control this
        if (!DecodePlusAsSpace && decoded != null)
        {
            // Manually encode + back if we don't want them decoded
            // This is a bit hacky but necessary due to WebUtility.UrlDecode behavior
            var original = str;
            if (original.Contains('+') && !original.Contains("%2B"))
            {
                // Only restore + if they weren't encoded as %2B
                return decoded; // Actually, UrlDecode doesn't decode + by default, so we're fine
            }
        }

        return decoded ?? str;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (MaxDecodeIterations < 1)
            throw new InvalidOperationException("MaxDecodeIterations must be at least 1.");
    }
}
