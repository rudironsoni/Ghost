namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for a field to extract.
/// </summary>
public sealed class FieldConfiguration
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field data type (String, Integer, Decimal, Boolean, DateTime, Url).
    /// </summary>
    public string Type { get; set; } = "String";

    /// <summary>
    /// Gets or sets the selector configuration.
    /// </summary>
    public SelectorConfiguration Selector { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this field is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default value if extraction fails.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets multiple selectors to try in order (fallback).
    /// </summary>
    public List<SelectorConfiguration> FallbackSelectors { get; set; } = new();

    /// <summary>
    /// Gets or sets formatters to apply to the extracted value.
    /// </summary>
    public List<FormatterConfiguration> Formatters { get; set; } = new();

    /// <summary>
    /// Gets or sets validation rules for this field.
    /// </summary>
    public FieldValidationConfiguration? Validation { get; set; }
}

/// <summary>
/// Validation configuration for fields.
/// </summary>
public sealed class FieldValidationConfiguration
{
    /// <summary>
    /// Gets or sets regex pattern the value must match.
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Gets or sets the minimum length for string values.
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum length for string values.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the minimum value for numeric types.
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value for numeric types.
    /// </summary>
    public decimal? MaxValue { get; set; }

    /// <summary>
    /// Gets or sets allowed values (enum).
    /// </summary>
    public List<string> AllowedValues { get; set; } = new();

    /// <summary>
    /// Gets or sets custom validation expressions.
    /// </summary>
    public List<string> CustomRules { get; set; } = new();
}
