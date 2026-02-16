using System.Xml.Linq;

namespace Ghost.Sdk.Spiders;

/// <summary>
/// Base spider class for content extraction.
/// </summary>
/// <remarks>
/// This is a simplified base class for spider implementations. Concrete spiders
/// should inherit from this class and override the appropriate methods.
/// </remarks>
public abstract class Spider
{
    /// <summary>
    /// Gets the name of this spider.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Called when the spider starts executing.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public virtual Task StartAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Downloads content from a URL.
    /// </summary>
    /// <param name="request">The request to download.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response containing the downloaded content.</returns>
    protected virtual async Task<Response> DownloadAsync(Request request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpClient = new HttpClient();
        httpClient.Timeout = request.Timeout;

        try
        {
            HttpResponseMessage httpResponse = await httpClient.GetAsync(request.Url, ct).ConfigureAwait(false);
            string content = await httpResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            return new Response
            {
                Url = request.Url,
                Body = content,
                StatusCode = (int)httpResponse.StatusCode,
                IsSuccess = httpResponse.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            return new Response
            {
                Url = request.Url,
                Body = string.Empty,
                StatusCode = 0,
                IsSuccess = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Schedules a request for processing.
    /// </summary>
    /// <param name="request">The request to schedule.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    protected virtual Task ScheduleRequestAsync(Request request, CancellationToken ct)
    {
        // Base implementation - subclasses should override to implement queueing
        return Task.CompletedTask;
    }
}

/// <summary>
/// Represents a request for content.
/// </summary>
public class Request
{
    /// <summary>
    /// Gets or sets the URL to request.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Represents a response from a content request.
/// </summary>
public class Response
{
    /// <summary>
    /// Gets or sets the requested URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response body.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the request was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the error message, if any.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Spider that discovers URLs from sitemap.xml files.
/// </summary>
/// <remarks>
/// This spider implementation parses XML sitemaps according to the sitemaps.org protocol.
/// It can handle both sitemap indexes (containing references to other sitemaps) and
/// URL sets (containing actual page URLs). The spider can recursively follow sitemap
/// indexes to discover all URLs in a site structure.
/// </remarks>
public class SitemapSpider : Spider, ISitemapSpider
{
    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private int _currentDepth;

    /// <summary>
    /// Gets the name of this spider.
    /// </summary>
    public override string Name => "SitemapSpider";

    /// <summary>
    /// Gets or sets the sitemap URL to start crawling from.
    /// </summary>
    public string SitemapUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sitemap options.
    /// </summary>
    public SitemapOptions Options { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapSpider"/> class.
    /// </summary>
    public SitemapSpider()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapSpider"/> class with a sitemap URL.
    /// </summary>
    /// <param name="sitemapUrl">The sitemap URL to crawl.</param>
    public SitemapSpider(string sitemapUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sitemapUrl);
        SitemapUrl = sitemapUrl;
    }

    /// <summary>
    /// Starts the spider execution.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method fetches the initial sitemap, parses it, and schedules all discovered
    /// URLs for processing. If the sitemap is a sitemap index, it will recursively
    /// follow nested sitemaps up to the configured MaxDepth.
    /// </remarks>
    public override async Task StartAsync(CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SitemapUrl);

        _currentDepth = 0;
        await ProcessSitemapAsync(SitemapUrl, ct).ConfigureAwait(false);
        await base.StartAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes a sitemap URL recursively.
    /// </summary>
    /// <param name="sitemapUrl">The sitemap URL to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    private async Task ProcessSitemapAsync(string sitemapUrl, CancellationToken ct)
    {
        if (_currentDepth >= Options.MaxDepth)
        {
            return;
        }

        // Fetch sitemap
        Response response = await DownloadAsync(new Request { Url = sitemapUrl }, ct).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            // Log error or handle failure
            return;
        }

        // Parse sitemap
        IEnumerable<string> urls = await ParseSitemapAsync(response.Body, ct).ConfigureAwait(false);

        // Process discovered URLs
        var urlList = urls.ToList();
        foreach (string? url in urlList)
        {
            // Check if this is a nested sitemap (if FollowSitemapIndex is enabled)
            if (Options.FollowSitemapIndex && IsSitemapUrl(url))
            {
                _currentDepth++;
                await ProcessSitemapAsync(url, ct).ConfigureAwait(false);
                _currentDepth--;
            }
            else
            {
                // Schedule regular URL for crawling
                await ScheduleRequestAsync(new Request { Url = url }, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Parses a sitemap XML document and extracts URLs.
    /// </summary>
    /// <param name="xmlContent">The XML content of the sitemap.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of URLs found in the sitemap.</returns>
    /// <remarks>
    /// This method handles both sitemap indexes and URL sets:
    /// <list type="bullet">
    /// <item>Sitemap Index: Extracts URLs from &lt;sitemap&gt;&lt;loc&gt; elements</item>
    /// <item>URL Set: Extracts URLs from &lt;url&gt;&lt;loc&gt; elements</item>
    /// </list>
    /// The method applies the LastModAfter filter if configured in options.
    /// </remarks>
    public Task<IEnumerable<string>> ParseSitemapAsync(string xmlContent, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlContent);

        var urls = new List<string>();

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var sitemapNamespace = XNamespace.Get(SitemapNamespace);

            // Handle sitemap index - extract nested sitemap URLs
            IEnumerable<XElement> sitemaps = doc.Descendants(sitemapNamespace + "sitemap");
            foreach (XElement sitemap in sitemaps)
            {
                string? urlLocation = sitemap.Element(sitemapNamespace + "loc")?.Value;
                if (!string.IsNullOrEmpty(urlLocation))
                {
                    urls.Add(urlLocation);
                }
            }

            // Handle URL set - extract page URLs
            IEnumerable<XElement> urlElements = doc.Descendants(sitemapNamespace + "url");
            foreach (XElement urlElement in urlElements)
            {
                string? urlLocation = urlElement.Element(sitemapNamespace + "loc")?.Value;
                if (string.IsNullOrEmpty(urlLocation))
                    continue;

                // Apply lastmod filter if configured
                if (Options.LastModAfter.HasValue)
                {
                    XElement? lastModElement = urlElement.Element(sitemapNamespace + "lastmod");
                    if (lastModElement is not null &&
                        DateTime.TryParse(lastModElement.Value, out DateTime lastMod))
                    {
                        TimeSpan age = DateTime.UtcNow - lastMod;
                        if (age <= Options.LastModAfter.Value)
                        {
                            urls.Add(urlLocation);
                        }
                    }
                }
                else
                {
                    urls.Add(urlLocation);
                }
            }
        }
        catch (System.Xml.XmlException)
        {
            // Invalid XML - return empty list
            // In production, you might want to log this error
        }

        return Task.FromResult<IEnumerable<string>>(urls);
    }

    /// <summary>
    /// Determines if a URL is likely a sitemap URL.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns><c>true</c> if the URL appears to be a sitemap; otherwise, <c>false</c>.</returns>
    private static bool IsSitemapUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        string urlLower = url.ToLowerInvariant();
        return urlLower.Contains("sitemap", StringComparison.Ordinal) && urlLower.EndsWith(".xml", StringComparison.Ordinal);
    }
}
