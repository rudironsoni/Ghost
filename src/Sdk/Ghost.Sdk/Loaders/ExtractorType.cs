namespace Ghost.Sdk.Loaders;

/// <summary>
/// Represents the type of extractor used for field extraction.
/// </summary>
internal enum ExtractorType
{
    /// <summary>
    /// XPath selector extraction.
    /// </summary>
    XPath,

    /// <summary>
    /// CSS selector extraction.
    /// </summary>
    Css,

    /// <summary>
    /// Static value assignment.
    /// </summary>
    Value
}
