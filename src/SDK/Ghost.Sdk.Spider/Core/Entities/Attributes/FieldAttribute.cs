namespace Ghost.Sdk.Spider.Core.Entities.Attributes;

/// <summary>
/// Provides additional configuration for a field including validation and transformation rules.
/// Applied to properties alongside <see cref="ValueSelectorAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class FieldAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the field name used in storage or output.
    /// If not specified, uses the property name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this field is required for entity validation.
    /// </summary>
    public bool Required { get; init; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to ignore this field if it's null or empty.
    /// </summary>
    public bool IgnoreNull { get; init; } = false;

    /// <summary>
    /// Gets or sets the maximum length for string values.
    /// Values exceeding this length will be truncated.
    /// </summary>
    public int MaxLength { get; init; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the minimum length for string values.
    /// Values shorter than this will fail validation if Required is true.
    /// </summary>
    public int MinLength { get; init; } = 0;

    /// <summary>
    /// Gets or sets a regular expression pattern for validation.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to remove HTML tags from the extracted value.
    /// </summary>
    public bool RemoveHtml { get; init; } = false;

    /// <summary>
    /// Gets or sets the order in which this field should be processed relative to other fields.
    /// Lower values are processed first.
    /// </summary>
    public int Order { get; init; } = int.MaxValue;
}
