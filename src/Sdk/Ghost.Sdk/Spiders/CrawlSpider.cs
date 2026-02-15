using Ghost.Sdk.Extraction;

namespace Ghost.Sdk.Spiders;

/// <summary>
/// Spider that follows links automatically based on configurable crawl rules.
/// </summary>
/// <remarks>
/// <para>
/// CrawlSpider extends the base Spider class to provide automatic link following
/// based on configurable rules. Each rule defines:
/// </para>
/// <list type="bullet">
/// <item>A condition for determining which URLs to follow</item>
/// <item>An action for extracting items from matched responses</item>
/// </list>
/// <para>
/// Rules are evaluated in the order they are added. When processing a response,
/// the spider:
/// </para>
/// <list type="number">
/// <item>Checks if the response URL matches any rule's follow condition</item>
/// <item>Executes the parse action of all matching rules to extract items</item>
/// <item>Extracts links from the response</item>
/// <item>Schedules links that match any rule's follow condition</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var linkExtractor = new HtmlAgilityLinkExtractor();
/// var spider = new CrawlSpider(linkExtractor) { Name = "ExampleSpider" };
///
/// // Add rule to parse product pages
/// spider.AddRule(
///     name: "ProductPages",
///     followCondition: url => url.Contains("/products/"),
///     parseAction: response => {
///         var item = new Item {
///             SourceUrl = response.Url,
///             Metadata = { ["title"] = "Extracted Title" }
///         };
///         return new[] { item };
///     }
/// );
///
/// // Add rule to follow category pages but not extract items
/// spider.AddRule(
///     name: "CategoryPages",
///     followCondition: url => url.Contains("/category/"),
///     parseAction: _ => Enumerable.Empty&lt;Item&gt;()
/// );
/// </code>
/// </example>
public class CrawlSpider : Spider, ICrawlSpider
{
    private readonly ILinkExtractor _linkExtractor;
    private readonly List<Request> _scheduledRequests = new();
    private readonly List<Item> _extractedItems = new();

    /// <summary>
    /// Gets the name of this spider.
    /// </summary>
    public override string Name { get; } = "CrawlSpider";

    /// <summary>
    /// Gets the collection of crawl rules.
    /// </summary>
    /// <value>List of rules that control crawling behavior.</value>
    public List<CrawlRule> Rules { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CrawlSpider"/> class.
    /// </summary>
    /// <param name="linkExtractor">The link extractor to use for discovering links.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="linkExtractor"/> is null.</exception>
    public CrawlSpider(ILinkExtractor linkExtractor)
    {
        ArgumentNullException.ThrowIfNull(linkExtractor);
        _linkExtractor = linkExtractor;
    }

    /// <summary>
    /// Adds a crawl rule to the spider.
    /// </summary>
    /// <param name="name">A descriptive name for the rule.</param>
    /// <param name="followCondition">A function that determines whether to follow a URL.</param>
    /// <param name="parseAction">A function that extracts items from a response.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="followCondition"/> or <paramref name="parseAction"/> is null.
    /// </exception>
    public void AddRule(string name, Func<string, bool> followCondition, Func<Response, IEnumerable<Item>> parseAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(followCondition);
        ArgumentNullException.ThrowIfNull(parseAction);

        Rules.Add(new CrawlRule
        {
            Name = name,
            FollowCondition = followCondition,
            ParseAction = parseAction
        });
    }

    /// <summary>
    /// Parses a response and extracts data according to matching rules.
    /// </summary>
    /// <param name="response">The response to parse.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method implements the core crawling logic:
    /// </para>
    /// <list type="number">
    /// <item>Finds rules that match the response URL</item>
    /// <item>Executes matching rule parse actions to extract items</item>
    /// <item>Extracts links from the response using the link extractor</item>
    /// <item>Schedules links that match any rule's follow condition</item>
    /// </list>
    /// </remarks>
    public async Task ParseAsync(Response response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);

        string url = response.Url;

        // Find and execute matching rules to extract items
        foreach (CrawlRule rule in Rules)
        {
            if (rule.FollowCondition(url))
            {
                IEnumerable<Item> items = rule.ParseAction(response);
                foreach (Item item in items)
                {
                    await YieldItemAsync(item, ct).ConfigureAwait(false);
                }
            }
        }

        // Extract and follow links
        if (response.IsSuccess && !string.IsNullOrWhiteSpace(response.Body))
        {
            IEnumerable<string> links = _linkExtractor.ExtractLinks(response.Body, url);

            foreach (string link in links)
            {
                // Schedule link if it matches any rule's follow condition
                if (Rules.Any(r => r.FollowCondition(link)))
                {
                    await ScheduleRequestAsync(new Request { Url = link }, ct).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Yields an extracted item for processing.
    /// </summary>
    /// <param name="item">The item to yield.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method stores the item for later retrieval. In a full spider engine
    /// implementation, this would typically send the item through processing pipelines.
    /// </remarks>
    protected virtual Task YieldItemAsync(Item item, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        _extractedItems.Add(item);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Schedules a request for processing.
    /// </summary>
    /// <param name="request">The request to schedule.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method stores the request for later retrieval. In a full spider engine
    /// implementation, this would typically add the request to a queue for processing.
    /// </remarks>
    protected override Task ScheduleRequestAsync(Request request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        _scheduledRequests.Add(request);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the requests that have been scheduled by this spider.
    /// </summary>
    /// <returns>A collection of scheduled requests.</returns>
    /// <remarks>
    /// This method allows retrieval of all requests scheduled during crawling.
    /// Useful for testing and for spider engine implementations that need to
    /// process discovered links.
    /// </remarks>
    public IEnumerable<Request> GetScheduledRequests()
    {
        return _scheduledRequests.AsReadOnly();
    }

    /// <summary>
    /// Gets the items that have been extracted by this spider.
    /// </summary>
    /// <returns>A collection of extracted items.</returns>
    /// <remarks>
    /// This method allows retrieval of all items extracted during crawling.
    /// Useful for testing and for spider engine implementations that need to
    /// process extracted data.
    /// </remarks>
    public IEnumerable<Item> GetExtractedItems()
    {
        return _extractedItems.AsReadOnly();
    }
}
