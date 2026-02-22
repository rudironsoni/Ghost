using System.Text.RegularExpressions;

namespace Ghost.Sdk.Extraction;

/// <summary>
/// Simple regex-based link extractor.
/// Fast but less accurate than proper HTML parsing.
/// </summary>
public sealed partial class RegexLinkExtractor : ILinkExtractor
{
    private readonly LinkExtractorOptions _options;

    [GeneratedRegex(@"<a[^>]+href=[""']([^""']+)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HrefPattern();

    public RegexLinkExtractor(LinkExtractorOptions? options = null)
    {
        _options = options ?? new LinkExtractorOptions();
    }

    public IEnumerable<string> ExtractLinks(string html, string baseUrl)
    {
        // Eager validation (before yield to ensure exceptions are thrown immediately)
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(baseUrl);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            throw new ArgumentException("Base URL must be a valid absolute URI.", nameof(baseUrl));
        }

        return ExtractLinksIterator(html, baseUri);
    }

    private IEnumerable<string> ExtractLinksIterator(string html, Uri baseUri)
    {
        MatchCollection matches = HrefPattern().Matches(html);
        HashSet<string> links = [];

        foreach (Match match in matches)
        {
            if (match.Groups.Count < 2)
            {
                continue;
            }

            string href = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryResolveUrl(baseUri, href, out string? absoluteUrl))
            {
                continue;
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

    private static bool TryResolveUrl(Uri baseUri, string href, out string absoluteUrl)
    {
        absoluteUrl = string.Empty;

        try
        {
            // Check if href is already an absolute URI with http/https scheme
            // Note: On Unix/Linux, paths starting with "/" are treated as absolute file URIs,
            // so we need to explicitly check for http/https schemes
            if (Uri.TryCreate(href, UriKind.Absolute, out Uri? uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                absoluteUrl = uri.GetLeftPart(UriPartial.Path) + uri.Query;
                return true;
            }

            // Resolve relative URI against base URI
            if (Uri.TryCreate(baseUri, href, out uri))
            {
                absoluteUrl = uri.GetLeftPart(UriPartial.Path) + uri.Query;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
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
            if (!_options.AllowedExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)))
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
