namespace Ghost.Sdk.Spider.Core.Entities.Attributes;

/// <summary>
/// Specifies how to select entity instances from the document.
/// Applied at the class level to define the scope of entity extraction.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class EntitySelectorAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the selector expression used to identify entity instances in the document.
    /// </summary>
    public required string Expression { get; init; }

    /// <summary>
    /// Gets or sets the type of selector (XPath, Css, Regex, JsonPath, JmesPath).
    /// </summary>
    public SelectorType Type { get; init; } = SelectorType.XPath;

    /// <summary>
    /// Gets or sets a value indicating whether to extract multiple entities or just the first match.
    /// </summary>
    public bool TakeFirst { get; init; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the selector is required.
    /// If true, extraction fails when no matches are found.
    /// </summary>
    public bool Required { get; init; } = true;
}

/// <summary>
/// Defines the types of selectors available for entity and value extraction.
/// </summary>
public enum SelectorType
{
    /// <summary>
    /// XPath selector for XML/HTML documents.
    /// </summary>
    XPath,

    /// <summary>
    /// CSS selector for HTML documents.
    /// </summary>
    Css,

    /// <summary>
    /// Regular expression selector.
    /// </summary>
    Regex,

    /// <summary>
    /// JSONPath selector for JSON documents.
    /// </summary>
    JsonPath,

    /// <summary>
    /// JMESPath selector for JSON documents (more powerful than JSONPath).
    /// </summary>
    JmesPath,

    /// <summary>
    /// Environment variable selector (special case for configuration).
    /// </summary>
    Environment
}
