using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Ghost.Sdk.Spider.Core.Extraction.Selectors;

/// <summary>
/// Selects values from HTML content using CSS selectors.
/// </summary>
public class CssSelector : ISelector
{
    private readonly HtmlParser _parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="CssSelector"/> class.
    /// </summary>
    /// <param name="expression">The CSS selector expression.</param>
    /// <param name="attribute">The attribute name to extract. If null, extracts text content.</param>
    public CssSelector(string expression, string? attribute = null)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Attribute = attribute;
        _parser = new HtmlParser();
    }

    /// <inheritdoc/>
    public string Expression { get; }

    /// <summary>
    /// Gets the attribute name to extract from selected elements.
    /// </summary>
    public string? Attribute { get; }

    /// <inheritdoc/>
    public List<string> SelectValues(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new List<string>();

        var document = _parser.ParseDocument(content);
        var elements = document.QuerySelectorAll(Expression);

        var results = new List<string>();
        foreach (var element in elements)
        {
            var value = ExtractValue(element);
            if (value != null)
                results.Add(value);
        }

        return results;
    }

    /// <inheritdoc/>
    public string? SelectFirst(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var document = _parser.ParseDocument(content);
        var element = document.QuerySelector(Expression);

        return element != null ? ExtractValue(element) : null;
    }

    /// <inheritdoc/>
    public bool Validate()
    {
        try
        {
            var document = _parser.ParseDocument("<html></html>");
            document.QuerySelector(Expression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string? ExtractValue(IElement element)
    {
        if (Attribute != null)
        {
            return element.GetAttribute(Attribute);
        }

        return element.TextContent;
    }
}
