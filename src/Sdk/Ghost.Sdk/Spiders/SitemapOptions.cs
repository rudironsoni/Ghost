namespace Ghost.Sdk.Spiders;

/// <summary>
/// Configuration options for sitemap spider behavior.
/// </summary>
public class SitemapOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to follow sitemap index files.
    /// </summary>
    /// <value>
    /// <c>true</c> to recursively process sitemap indexes; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, the spider will follow &lt;sitemap&gt; entries in sitemap index files
    /// to discover nested sitemaps. This allows complete site discovery from a single
    /// sitemap index URL.
    /// </remarks>
    public bool FollowSitemapIndex { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum depth for following sitemap indexes.
    /// </summary>
    /// <value>The maximum recursion depth. Defaults to 3.</value>
    /// <remarks>
    /// This prevents infinite loops in malformed sitemap structures and limits
    /// the resource usage of deeply nested sitemap indexes. A value of 1 means
    /// only the root sitemap will be processed. A value of 3 allows for
    /// root → level 1 → level 2 → level 3 sitemap traversal.
    /// </remarks>
    public int MaxDepth { get; set; } = 3;

    /// <summary>
    /// Gets or sets a filter for URLs based on last modification time.
    /// </summary>
    /// <value>
    /// Only URLs with &lt;lastmod&gt; values after this timestamp will be included.
    /// Null means no filtering. Defaults to null.
    /// </value>
    /// <remarks>
    /// This is useful for incremental crawling where you only want to discover
    /// URLs that have been updated since your last crawl. The filter is applied
    /// to &lt;lastmod&gt; elements in &lt;url&gt; entries.
    /// </remarks>
    public TimeSpan? LastModAfter { get; set; }
}
