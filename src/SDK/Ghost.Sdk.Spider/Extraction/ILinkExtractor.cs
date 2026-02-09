namespace Ghost.Sdk.Spider.Extraction;

/// <summary>
/// Defines the contract for extracting links from HTML content.
/// </summary>
public interface ILinkExtractor
{
    /// <summary>
    /// Extracts links from the provided HTML content.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <param name="baseUrl">The base URL for resolving relative links.</param>
    /// <returns>A collection of absolute URLs extracted from the HTML.</returns>
    IEnumerable<string> ExtractLinks(string html, string baseUrl);
}
