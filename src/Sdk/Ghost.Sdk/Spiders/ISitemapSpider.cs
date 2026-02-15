namespace Ghost.Sdk.Spiders;

/// <summary>
/// Spider that discovers URLs from sitemap.xml files.
/// </summary>
/// <remarks>
/// This spider is designed to parse XML sitemaps (both sitemap indexes and URL sets)
/// according to the sitemap protocol (https://www.sitemaps.org/protocol.html).
/// It can recursively follow sitemap indexes to discover all URLs in a site.
/// </remarks>
public interface ISitemapSpider
{
    /// <summary>
    /// Gets or sets the sitemap URL to start crawling from.
    /// </summary>
    /// <value>The URL of the sitemap.xml or sitemap index file.</value>
    /// <remarks>
    /// This should be an absolute URL pointing to a sitemap XML file.
    /// Common locations include:
    /// <list type="bullet">
    /// <item>/sitemap.xml</item>
    /// <item>/sitemap_index.xml</item>
    /// <item>/sitemap-index.xml</item>
    /// </list>
    /// </remarks>
    public string SitemapUrl { get; set; }

    /// <summary>
    /// Parses a sitemap XML document and extracts URLs.
    /// </summary>
    /// <param name="xmlContent">The XML content of the sitemap.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of URLs found in the sitemap.</returns>
    /// <remarks>
    /// This method handles both sitemap indexes (containing references to other sitemaps)
    /// and URL sets (containing actual page URLs). The method should extract URLs from
    /// &lt;loc&gt; elements in both &lt;sitemap&gt; and &lt;url&gt; entries.
    /// </remarks>
    public Task<IEnumerable<string>> ParseSitemapAsync(string xmlContent, CancellationToken ct);
}
