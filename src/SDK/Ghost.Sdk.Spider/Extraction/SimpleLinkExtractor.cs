using HtmlAgilityPack;

namespace Ghost.Sdk.Spider.Extraction;

/// <summary>
/// Simple link extractor using HtmlAgilityPack for HTML parsing.
/// </summary>
public sealed class SimpleLinkExtractor : ILinkExtractor
{
    /// <summary>
    /// Extracts links from the provided HTML content.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <param name="baseUrl">The base URL for resolving relative links.</param>
    /// <returns>A collection of absolute URLs extracted from the HTML.</returns>
    public IEnumerable<string> ExtractLinks(string html, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(baseUrl);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException("Base URL must be a valid absolute URI.", nameof(baseUrl));
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (nodes is null)
        {
            yield break;
        }

        var seen = new HashSet<string>();

        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryResolveUrl(baseUri, href, out var absoluteUrl))
            {
                continue;
            }

            if (seen.Add(absoluteUrl))
            {
                yield return absoluteUrl;
            }
        }
    }

    private static bool TryResolveUrl(Uri baseUri, string href, out string absoluteUrl)
    {
        absoluteUrl = string.Empty;

        try
        {
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
            {
                absoluteUrl = uri.AbsoluteUri;
                return true;
            }

            if (Uri.TryCreate(baseUri, href, out uri))
            {
                absoluteUrl = uri.AbsoluteUri;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
