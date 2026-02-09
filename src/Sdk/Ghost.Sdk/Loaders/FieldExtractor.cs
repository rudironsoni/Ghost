namespace Ghost.Sdk.Loaders;

/// <summary>
/// Represents a field extraction configuration.
/// </summary>
internal sealed class FieldExtractor
{
    /// <summary>
    /// Gets or sets the type of extraction.
    /// </summary>
    public required ExtractorType Type { get; set; }

    /// <summary>
    /// Gets or sets the selector (XPath or CSS) or static value.
    /// </summary>
    public required string Selector { get; set; }
}
