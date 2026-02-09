using System.Reflection;
using HtmlAgilityPack;

namespace Ghost.Sdk.Loaders;

/// <summary>
/// Implementation of item loader that provides declarative extraction and transformation of scraped data.
/// </summary>
/// <typeparam name="T">The type of item to load. Must be a class with a parameterless constructor.</typeparam>
public sealed class ItemLoader<T> : IItemLoader<T> where T : class, new()
{
    private readonly Dictionary<string, List<FieldExtractor>> _extractors = new();
    private readonly Dictionary<string, List<Func<string, string>>> _processors = new();

    /// <summary>
    /// Adds a field extractor using an XPath selector.
    /// </summary>
    /// <param name="field">The name of the property to populate.</param>
    /// <param name="xpath">The XPath selector to extract values.</param>
    /// <returns>The current loader instance for method chaining.</returns>
    public IItemLoader<T> AddXPath(string field, string xpath)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(xpath);

        GetOrAddExtractors(field).Add(new FieldExtractor
        {
            Type = ExtractorType.XPath,
            Selector = xpath
        });

        return this;
    }

    /// <summary>
    /// Adds a field extractor using a CSS selector.
    /// </summary>
    /// <param name="field">The name of the property to populate.</param>
    /// <param name="selector">The CSS selector to extract values.</param>
    /// <returns>The current loader instance for method chaining.</returns>
    public IItemLoader<T> AddCss(string field, string selector)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(selector);

        GetOrAddExtractors(field).Add(new FieldExtractor
        {
            Type = ExtractorType.Css,
            Selector = selector
        });

        return this;
    }

    /// <summary>
    /// Adds a static value to a field.
    /// </summary>
    /// <param name="field">The name of the property to populate.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The current loader instance for method chaining.</returns>
    public IItemLoader<T> AddValue(string field, string value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);

        GetOrAddExtractors(field).Add(new FieldExtractor
        {
            Type = ExtractorType.Value,
            Selector = value
        });

        return this;
    }

    /// <summary>
    /// Adds a processor function to transform a field's value.
    /// </summary>
    /// <param name="field">The name of the property to process.</param>
    /// <param name="processor">The transformation function.</param>
    /// <returns>The current loader instance for method chaining.</returns>
    public IItemLoader<T> AddProcessor(string field, Func<string, string> processor)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(processor);

        GetOrAddProcessors(field).Add(processor);

        return this;
    }

    /// <summary>
    /// Extracts data from HTML and returns a single populated item.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>A populated item instance.</returns>
    public T LoadItem(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var item = new T();

        foreach (var (field, extractors) in _extractors)
        {
            var values = new List<string>();

            foreach (var extractor in extractors)
            {
                var value = extractor.Type switch
                {
                    ExtractorType.XPath => ExtractXPath(doc, extractor.Selector),
                    ExtractorType.Css => ExtractCss(doc, extractor.Selector),
                    ExtractorType.Value => extractor.Selector,
                    _ => null
                };

                if (value is not null)
                {
                    values.Add(value);
                }
            }

            var finalValue = string.Join(", ", values);

            if (_processors.TryGetValue(field, out var processors))
            {
                finalValue = processors.Aggregate(finalValue, (current, processor) => processor(current));
            }

            SetPropertyValue(item, field, finalValue);
        }

        return item;
    }

    /// <summary>
    /// Extracts data from HTML and returns multiple populated items.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>A list of populated item instances.</returns>
    public List<T> LoadItems(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        // For simplicity, LoadItems returns a single-item list
        // In a full implementation, this would support extracting multiple items from repeated HTML structures
        return [LoadItem(html)];
    }

    private static string? ExtractXPath(HtmlDocument doc, string xpath)
    {
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        return node?.InnerText;
    }

    private static string? ExtractCss(HtmlDocument doc, string selector)
    {
        // HtmlAgilityPack doesn't have native CSS selector support
        // We'll use QuerySelector extension method if available, or convert to XPath
        // For now, we'll use a simple implementation that handles basic CSS selectors
        var node = doc.DocumentNode.SelectSingleNode(ConvertCssToXPath(selector));
        return node?.InnerText;
    }

    private static string ConvertCssToXPath(string cssSelector)
    {
        // Basic CSS to XPath conversion for common patterns
        // This is a simplified implementation
        if (cssSelector.StartsWith('.'))
        {
            // Class selector: .class -> //*[contains(@class, 'class')]
            var className = cssSelector[1..];
            return $"//*[contains(@class, '{className}')]";
        }

        if (cssSelector.StartsWith('#'))
        {
            // ID selector: #id -> //*[@id='id']
            var id = cssSelector[1..];
            return $"//*[@id='{id}']";
        }

        // Element selector: tag -> //tag
        return $"//{cssSelector}";
    }

    private static void SetPropertyValue(T item, string propertyName, string value)
    {
        var prop = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        if (prop is null || !prop.CanWrite)
        {
            return;
        }

        prop.SetValue(item, value);
    }

    private List<FieldExtractor> GetOrAddExtractors(string field)
    {
        if (!_extractors.TryGetValue(field, out var extractors))
        {
            extractors = [];
            _extractors[field] = extractors;
        }

        return extractors;
    }

    private List<Func<string, string>> GetOrAddProcessors(string field)
    {
        if (!_processors.TryGetValue(field, out var processors))
        {
            processors = [];
            _processors[field] = processors;
        }

        return processors;
    }
}
