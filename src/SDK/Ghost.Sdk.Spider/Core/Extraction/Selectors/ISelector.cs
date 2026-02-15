namespace Ghost.Sdk.Spider.Core.Extraction.Selectors;

/// <summary>
/// Defines the contract for all selector implementations that extract values from content.
/// </summary>
public interface ISelector
{
    /// <summary>
    /// Gets the selector expression.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Selects values from the content using the selector expression.
    /// </summary>
    /// <param name="content">The content to select from.</param>
    /// <returns>A list of selected string values.</returns>
    public List<string> SelectValues(string content);

    /// <summary>
    /// Selects a single value from the content using the selector expression.
    /// </summary>
    /// <param name="content">The content to select from.</param>
    /// <returns>The first selected value, or null if no match is found.</returns>
    public string? SelectFirst(string content);

    /// <summary>
    /// Validates whether the selector expression is valid.
    /// </summary>
    /// <returns>True if the selector is valid; otherwise, false.</returns>
    public bool Validate();
}
