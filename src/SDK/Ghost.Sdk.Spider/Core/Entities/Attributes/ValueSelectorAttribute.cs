namespace Ghost.Sdk.Spider.Core.Entities.Attributes;

/// <summary>
/// Specifies how to extract a value from the selected entity node.
/// Applied to properties to define value extraction rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public class ValueSelectorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueSelectorAttribute"/> class.
    /// </summary>
    /// <param name="expression">The selector expression to extract the value.</param>
    /// <param name="type">The type of selector.</param>
    public ValueSelectorAttribute(string expression, SelectorType type = SelectorType.XPath)
    {
        Expression = expression;
        Type = type;
    }

    /// <summary>
    /// Gets the selector expression used to extract the value.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Gets the type of selector (XPath, Css, Regex, JsonPath, JmesPath).
    /// </summary>
    public SelectorType Type { get; }

    /// <summary>
    /// Gets or sets the attribute name to extract from the selected element.
    /// If null, extracts the text content.
    /// </summary>
    public string? Attribute { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to extract only the first match or all matches.
    /// </summary>
    public bool TakeFirst { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this value is required.
    /// If true, extraction fails when no value is found.
    /// </summary>
    public bool Required { get; init; } = false;

    /// <summary>
    /// Gets or sets the default value to use when no match is found.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to extract values from the outerHTML instead of innerText.
    /// </summary>
    public bool OuterHtml { get; init; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to extract values from the innerHTML instead of innerText.
    /// </summary>
    public bool InnerHtml { get; init; } = false;
}
