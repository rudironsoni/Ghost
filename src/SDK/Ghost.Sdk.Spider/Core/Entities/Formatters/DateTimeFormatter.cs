using System.Globalization;

namespace Ghost.Sdk.Spider.Core.Entities.Formatters;

/// <summary>
/// Parses and formats DateTime values with customizable input/output formats.
/// </summary>
/// <remarks>
/// This formatter handles DateTime parsing from strings and formatting to specified output formats.
/// It supports culture-specific parsing and formatting, custom date formats, and handles
/// both DateTime and DateTimeOffset types.
/// </remarks>
/// <example>
/// <code>
/// // Parse custom format and output as ISO 8601
/// var formatter = new DateTimeFormatter 
/// { 
///     InputFormat = "MM/dd/yyyy",
///     OutputFormat = "yyyy-MM-dd"
/// };
/// var result = formatter.Format("12/25/2024"); // Returns "2024-12-25"
/// 
/// // Parse with specific culture
/// var frenchFormatter = new DateTimeFormatter 
/// { 
///     Culture = "fr-FR",
///     OutputFormat = "D"
/// };
/// var result2 = frenchFormatter.Format("25/12/2024"); // Returns "mercredi 25 décembre 2024"
/// </code>
/// </example>
public class DateTimeFormatter : Formatter
{
    /// <summary>
    /// Gets or sets the input format for parsing DateTime strings.
    /// If null, uses default parsing.
    /// </summary>
    /// <value>A DateTime format string (e.g., "yyyy-MM-dd", "MM/dd/yyyy HH:mm:ss").</value>
    public string? InputFormat { get; set; }

    /// <summary>
    /// Gets or sets the output format for formatting DateTime values.
    /// If null, returns the DateTime object unchanged.
    /// </summary>
    /// <value>A DateTime format string or standard format specifier.</value>
    public string? OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets the culture name for parsing and formatting (e.g., "en-US", "fr-FR").
    /// If null, uses invariant culture.
    /// </summary>
    /// <value>A culture name string.</value>
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets the DateTimeStyles for parsing.
    /// </summary>
    /// <value>The parsing options. Defaults to <see cref="DateTimeStyles.None"/>.</value>
    public DateTimeStyles DateTimeStyles { get; set; } = DateTimeStyles.None;

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

        var culture = GetCultureInfo();
        DateTime dateTime;

        if (value is DateTime dt)
        {
            dateTime = dt;
        }
        else if (value is DateTimeOffset dto)
        {
            dateTime = dto.DateTime;
        }
        else if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return null;

            if (InputFormat != null)
            {
                if (!DateTime.TryParseExact(str, InputFormat, culture, DateTimeStyles, out dateTime))
                    return value; // Return original if parsing fails
            }
            else
            {
                if (!DateTime.TryParse(str, culture, DateTimeStyles, out dateTime))
                    return value; // Return original if parsing fails
            }
        }
        else
        {
            return value;
        }

        if (OutputFormat != null)
        {
            return dateTime.ToString(OutputFormat, culture);
        }

        return dateTime;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
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

        if (OutputFormat != null)
        {
            try
            {
                // Test format string with a sample date
                _ = DateTime.Now.ToString(OutputFormat, GetCultureInfo());
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"Invalid output format: {OutputFormat}", ex);
            }
        }
    }
}
