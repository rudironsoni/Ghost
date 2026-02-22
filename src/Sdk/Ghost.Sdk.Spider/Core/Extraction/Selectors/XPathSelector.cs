using HtmlAgilityPack;

namespace Ghost.Sdk.Spider.Core.Extraction.Selectors;

/// <summary>
/// Selects values from HTML/XML content using XPath expressions.
/// </summary>
public class XPathSelector : ISelector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XPathSelector"/> class.
    /// </summary>
    /// <param name="expression">The XPath expression.</param>
    /// <param name="attribute">The attribute name to extract. If null, extracts text content.</param>
    public XPathSelector(string expression, string? attribute = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Expression = expression;
        Attribute = attribute;
    }

    /// <inheritdoc/>
    public string Expression { get; }

    /// <summary>
    /// Gets the attribute name to extract from selected nodes.
    /// </summary>
    public string? Attribute { get; }

    /// <inheritdoc/>
    public List<string> SelectValues(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new List<string>();

        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        // Try the expression as-is first
        HtmlNodeCollection? nodes = doc.DocumentNode.SelectNodes(Expression);

        // If no results and expression starts with @, try from first element
        if ((nodes == null || nodes.Count == 0) && Expression.StartsWith('@'))
        {
            HtmlNode firstElement = doc.DocumentNode.FirstChild;
            if (firstElement != null && firstElement.NodeType == HtmlNodeType.Element)
            {
                nodes = firstElement.SelectNodes(Expression);
            }
        }

        if (nodes == null || nodes.Count == 0)
            return new List<string>();

        List<string> results = [];
        foreach (HtmlNode? node in nodes)
        {
            string? value = ExtractValue(node);
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

        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        // Try the expression as-is first
        HtmlNode? node = doc.DocumentNode.SelectSingleNode(Expression);

        // If no result and expression starts with @, try from first element
        if (node == null && Expression.StartsWith('@'))
        {
            HtmlNode firstElement = doc.DocumentNode.FirstChild;
            if (firstElement != null && firstElement.NodeType == HtmlNodeType.Element)
            {
                node = firstElement.SelectSingleNode(Expression);
            }
        }

        return node != null ? ExtractValue(node) : null;
    }

    /// <inheritdoc/>
    public bool Validate()
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml("<html></html>");
            doc.DocumentNode.SelectSingleNode(Expression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string? ExtractValue(HtmlNode node)
    {
        // If the Attribute parameter is set, extract that attribute
        if (Attribute != null)
        {
            return node.GetAttributeValue(Attribute, null);
        }

        // If the XPath expression is selecting an attribute (starts with @),
        // we need to extract the attribute value from the returned element
        if (Expression.StartsWith('@'))
        {
            // Extract the attribute name from expressions like "@data-id" or "@href"
            string attrName = Expression.TrimStart('@');
            // Handle more complex cases like "@data-id" with predicates
            int spaceIndex = attrName.IndexOf('[');
            if (spaceIndex > 0)
                attrName = attrName.Substring(0, spaceIndex);

            return node.GetAttributeValue(attrName, null);
        }

        return node.InnerText;
    }
}
