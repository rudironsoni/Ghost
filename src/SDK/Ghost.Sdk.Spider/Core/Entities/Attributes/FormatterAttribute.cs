namespace Ghost.Sdk.Spider.Core.Entities.Attributes;

/// <summary>
/// Base attribute for all formatter attributes that transform extracted values.
/// Formatters are applied in the order they are declared on a property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public abstract class FormatterAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the order in which this formatter should be applied.
    /// Lower values are applied first.
    /// </summary>
    public int Order { get; init; } = int.MaxValue;

    /// <summary>
    /// Formats the input value according to the formatter's rules.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted value.</returns>
    public abstract object? Format(object? value);
}

/// <summary>
/// Trims whitespace from string values.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class TrimFormatterAttribute : FormatterAttribute
{
    /// <summary>
    /// Gets or sets the characters to trim. If null, trims whitespace.
    /// </summary>
    public string? TrimChars { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to trim only the start of the string.
    /// </summary>
    public bool TrimStart { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to trim only the end of the string.
    /// </summary>
    public bool TrimEnd { get; init; }

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

/// <summary>
/// Replaces text in string values.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public class ReplaceFormatterAttribute : FormatterAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceFormatterAttribute"/> class.
    /// </summary>
    /// <param name="oldValue">The string to replace.</param>
    /// <param name="newValue">The replacement string.</param>
    public ReplaceFormatterAttribute(string oldValue, string newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>
    /// Gets the string to replace.
    /// </summary>
    public string OldValue { get; }

    /// <summary>
    /// Gets the replacement string.
    /// </summary>
    public string NewValue { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the replacement is case-insensitive.
    /// </summary>
    public bool IgnoreCase { get; init; }

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value is not string str)
            return value;

        if (string.IsNullOrEmpty(str))
            return str;

        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return str.Replace(OldValue, NewValue, comparison);
    }
}

/// <summary>
/// Applies a regular expression transformation to string values.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public class RegexFormatterAttribute : FormatterAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegexFormatterAttribute"/> class.
    /// </summary>
    /// <param name="pattern">The regular expression pattern.</param>
    public RegexFormatterAttribute(string pattern)
    {
        Pattern = pattern;
    }

    /// <summary>
    /// Gets the regular expression pattern.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Gets or sets the replacement string. If null, extracts the first match.
    /// Supports capture groups like $1, $2, etc.
    /// </summary>
    public string? Replacement { get; init; }

    /// <summary>
    /// Gets or sets the capture group index to extract (0 = whole match).
    /// Only used when Replacement is null.
    /// </summary>
    public int Group { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the pattern is case-insensitive.
    /// </summary>
    public bool IgnoreCase { get; init; }

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value is not string str)
            return value;

        if (string.IsNullOrEmpty(str))
            return str;

        var options = IgnoreCase ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None;
        var regex = new System.Text.RegularExpressions.Regex(Pattern, options);

        if (Replacement != null)
        {
            return regex.Replace(str, Replacement);
        }

        var match = regex.Match(str);
        if (!match.Success)
            return str;

        return match.Groups[Group].Value;
    }
}

/// <summary>
/// Parses and formats DateTime values.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class DateTimeFormatterAttribute : FormatterAttribute
{
    /// <summary>
    /// Gets or sets the input format for parsing DateTime strings.
    /// If null, uses default parsing.
    /// </summary>
    public string? InputFormat { get; init; }

    /// <summary>
    /// Gets or sets the output format for formatting DateTime values.
    /// If null, uses ISO 8601 format.
    /// </summary>
    public string? OutputFormat { get; init; }

    /// <summary>
    /// Gets or sets the culture name for parsing and formatting (e.g., "en-US", "fr-FR").
    /// If null, uses invariant culture.
    /// </summary>
    public string? Culture { get; init; }

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value == null)
            return null;

        var culture = Culture != null
            ? System.Globalization.CultureInfo.GetCultureInfo(Culture)
            : System.Globalization.CultureInfo.InvariantCulture;

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
                if (!DateTime.TryParseExact(str, InputFormat, culture, System.Globalization.DateTimeStyles.None, out dateTime))
                    return value;
            }
            else
            {
                if (!DateTime.TryParse(str, culture, System.Globalization.DateTimeStyles.None, out dateTime))
                    return value;
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
}

/// <summary>
/// Formats values using composite string formatting.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class StringFormatterAttribute : FormatterAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StringFormatterAttribute"/> class.
    /// </summary>
    /// <param name="format">The composite format string (e.g., "Value: {0}", "{0:N2}").</param>
    public StringFormatterAttribute(string format)
    {
        FormatString = format;
    }

    /// <summary>
    /// Gets the composite format string.
    /// </summary>
    public string FormatString { get; }

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value == null)
            return null;

        try
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, FormatString, value);
        }
        catch
        {
            return value;
        }
    }
}

/// <summary>
/// Converts string values to lowercase.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class LowercaseFormatterAttribute : FormatterAttribute
{
    /// <summary>
    /// Gets or sets the culture name for lowercase conversion (e.g., "en-US", "tr-TR").
    /// If null, uses invariant culture.
    /// </summary>
    public string? Culture { get; init; }

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value is not string str)
            return value;

        var culture = Culture != null
            ? System.Globalization.CultureInfo.GetCultureInfo(Culture)
            : System.Globalization.CultureInfo.InvariantCulture;

        return str.ToLower(culture);
    }
}

/// <summary>
/// Converts string values to uppercase.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class UppercaseFormatterAttribute : FormatterAttribute
{
    /// <summary>
    /// Gets or sets the culture name for uppercase conversion (e.g., "en-US", "tr-TR").
    /// If null, uses invariant culture.
    /// </summary>
    public string? Culture { get; init; }

    /// <inheritdoc/>
    public override object? Format(object? value)
    {
        if (value is not string str)
            return value;

        var culture = Culture != null
            ? System.Globalization.CultureInfo.GetCultureInfo(Culture)
            : System.Globalization.CultureInfo.InvariantCulture;

        return str.ToUpper(culture);
    }
}
