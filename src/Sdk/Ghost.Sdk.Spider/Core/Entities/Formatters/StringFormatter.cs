using System.Globalization;

namespace Ghost.Sdk.Spider.Core.Entities.Formatters;

/// <summary>
/// Formats values using composite string formatting.
/// </summary>
/// <remarks>
/// This formatter applies string.Format-style formatting to values, supporting
/// all standard and custom format specifiers. It's particularly useful for
/// adding prefixes/suffixes or formatting numeric values.
/// </remarks>
/// <example>
/// <code>
/// // Add currency formatting
/// var currencyFormatter = new StringFormatter { FormatString = "${0:N2}" };
/// var price = currencyFormatter.Format(1234.5); // Returns "$1,234.50"
///
/// // Add prefix
/// var prefixFormatter = new StringFormatter { FormatString = "Item: {0}" };
/// var item = prefixFormatter.Format("Widget"); // Returns "Item: Widget"
///
/// // Format percentage
/// var percentFormatter = new StringFormatter { FormatString = "{0:P1}" };
/// var percent = percentFormatter.Format(0.755); // Returns "75.5%"
/// </code>
/// </example>
public class StringFormatter : Formatter
{
    /// <summary>
    /// Gets or sets the composite format string (e.g., "Value: {0}", "{0:N2}", "({0})").
    /// </summary>
    /// <value>A format string using {0} as the placeholder for the value.</value>
    public required string FormatString { get; set; }

    /// <summary>
    /// Gets or sets the culture name for formatting (e.g., "en-US", "de-DE").
    /// If null, uses invariant culture.
    /// </summary>
    /// <value>A culture name string.</value>
    public string? Culture { get; set; }

    private CultureInfo GetCultureInfo()
    {
        return Culture != null
            ? CultureInfo.GetCultureInfo(Culture)
            : CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value == null)
            return null;

        try
        {
            return string.Format(GetCultureInfo(), FormatString, value);
        }
        catch (FormatException)
        {
            // If formatting fails, return original value
            return value;
        }
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (string.IsNullOrWhiteSpace(FormatString))
            throw new InvalidOperationException("FormatString cannot be null or whitespace.");

        if (!FormatString.Contains("{0}"))
            throw new InvalidOperationException("FormatString must contain {0} placeholder.");

        if (Culture != null)
        {
            try
            {
                _ = CultureInfo.GetCultureInfo(Culture);
            }
            catch (CultureNotFoundException ex)
            {
                throw new InvalidOperationException($"Invalid culture: {Culture}", ex);
            }
        }

        // Test format string with a sample value
        try
        {
            _ = string.Format(GetCultureInfo(), FormatString, "test");
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Invalid format string: {FormatString}", ex);
        }
    }
}
