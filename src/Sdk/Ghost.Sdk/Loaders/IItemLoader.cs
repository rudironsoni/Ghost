namespace Ghost.Sdk.Loaders;

/// <summary>
/// Defines the contract for item loaders that extract and transform scraped data declaratively.
/// </summary>
/// <typeparam name="T">The type of item to load. Must be a class with a parameterless constructor.</typeparam>
public interface IItemLoader<T> where T : class, new()
{
    /// <summary>
    /// Adds a field extractor using an XPath selector.
    /// </summary>
    /// <param name="field">The name of the property to populate.</param>
    /// <param name="xpath">The XPath selector to extract values.</param>
    /// <returns>The current loader instance for method chaining.</returns>
    public IItemLoader<T> AddXPath(string field, string xpath);

    /// <summary>
    /// Adds a field extractor using a CSS selector.
    /// </summary>
    /// <param name="field">The name of the property to populate.</param>
    /// <param name="selector">The CSS selector to extract values.</param>
    /// <returns>The current loader instance for method chaining.</returns>
    public IItemLoader<T> AddCss(string field, string selector);

    /// <summary>
    /// Adds a static value to a field.
    /// </summary>
    /// <param name="field">The name of the property to populate.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The current loader instance for method chaining.</returns>
    public IItemLoader<T> AddValue(string field, string value);

    /// <summary>
    /// Adds a processor function to transform a field's value.
    /// </summary>
    /// <param name="field">The name of the property to process.</param>
    /// <param name="processor">The transformation function.</param>
    /// <returns>The current loader instance for method chaining.</returns>
    public IItemLoader<T> AddProcessor(string field, Func<string, string> processor);

    /// <summary>
    /// Extracts data from HTML and returns a single populated item.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>A populated item instance.</returns>
    public T LoadItem(string html);

    /// <summary>
    /// Extracts data from HTML and returns multiple populated items.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>A list of populated item instances.</returns>
    public List<T> LoadItems(string html);
}
