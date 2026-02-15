using HtmlAgilityPack;

namespace Ghost.Sdk.Extraction;

/// <summary>
/// Link extractor using HtmlAgilityPack for proper HTML parsing.
/// Handles malformed HTML gracefully and supports XPath selectors.
/// </summary>
public sealed class HtmlAgilityLinkExtractor : ILinkExtractor
{
    private readonly LinkExtractorOptions _options;

    public HtmlAgilityLinkExtractor(LinkExtractorOptions? options = null)
    {
        _options = options ?? new LinkExtractorOptions();
    }

    public IEnumerable<string> ExtractLinks(string html, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(baseUrl);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            throw new ArgumentException("Base URL must be a valid absolute URI.", nameof(baseUrl));
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        IEnumerable<HtmlNode> nodes = GetTargetNodes(doc);
        var links = new HashSet<string>();

        foreach (HtmlNode node in nodes)
        {
            string href = node.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryResolveUrl(baseUri, href, out string? absoluteUrl))
            {
                continue;
            }

            if (_options.StripFragments)
            {
                absoluteUrl = StripFragment(absoluteUrl);
            }

            if (!PassesFilters(absoluteUrl))
            {
                continue;
            }

            if (_options.UniqueOnly)
            {
                links.Add(absoluteUrl);
            }
            else
            {
                yield return absoluteUrl;
            }
        }

        if (_options.UniqueOnly)
        {
            foreach (string link in links)
            {
                yield return link;
            }
        }
    }

    private IEnumerable<HtmlNode> GetTargetNodes(HtmlDocument doc)
    {
        // If XPath restrictions are specified, only search within those nodes
        if (_options.RestrictXpaths is not null && _options.RestrictXpaths.Count > 0)
        {
            foreach (string xpath in _options.RestrictXpaths)
            {
                HtmlNodeCollection? containerNodes = doc.DocumentNode.SelectNodes(xpath);
                if (containerNodes is not null)
                {
                    foreach (HtmlNode? container in containerNodes)
                    {
                        HtmlNodeCollection? linkNodes = container.SelectNodes(".//a[@href]");
                        if (linkNodes is not null)
                        {
                            foreach (HtmlNode? node in linkNodes)
                            {
                                yield return node;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // Extract all <a> tags with href attribute
            HtmlNodeCollection? nodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (nodes is not null)
            {
                foreach (HtmlNode? node in nodes)
                {
                    yield return node;
                }
            }
        }
    }

    private static bool TryResolveUrl(Uri baseUri, string href, out string absoluteUrl)
    {
        absoluteUrl = string.Empty;

        try
        {
            if (Uri.TryCreate(href, UriKind.Absolute, out Uri? uri))
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

    private static string StripFragment(string url)
    {
        int fragmentIndex = url.IndexOf('#');
        return fragmentIndex >= 0 ? url[..fragmentIndex] : url;
    }

    private bool PassesFilters(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        // Check denied extensions
        if (_options.DenyExtensions is not null && _options.DenyExtensions.Count > 0)
        {
            string extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (_options.DenyExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // Check allowed extensions
        if (_options.AllowedExtensions is not null && _options.AllowedExtensions.Count > 0)
        {
            string extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            // Empty string means no extension
            if (string.IsNullOrEmpty(extension))
            {
                if (!_options.AllowedExtensions.Contains(string.Empty))
                {
                    return false;
                }
            }
            else if (!_options.AllowedExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // Check allowed domains
        if (_options.AllowedDomains is not null && _options.AllowedDomains.Count > 0)
        {
            string host = uri.Host.ToLowerInvariant();
            if (!_options.AllowedDomains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }
}
