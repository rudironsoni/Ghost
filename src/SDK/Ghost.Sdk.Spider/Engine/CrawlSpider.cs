using System.Text.RegularExpressions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Extraction;

namespace Ghost.Sdk.Spider.Engine;

/// <summary>
/// Spider that follows links automatically based on rules.
/// </summary>
/// <remarks>
/// <para>
/// CrawlSpider extends the base Spider class to provide automatic link following
/// based on configurable rules. Each rule defines:
/// </para>
/// <list type="bullet">
/// <item>A regex pattern to match URLs</item>
/// <item>A callback function to extract items from matched pages</item>
/// <item>Whether to follow links found on matched pages</item>
/// </list>
/// <para>
/// Rules are evaluated in order. When a response URL matches a rule's pattern,
/// the rule's callback is invoked to extract items. If the rule has Follow=true,
/// links are extracted from the page and scheduled for crawling.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var spider = new MyCrawlSpider(linkExtractor);
/// spider.AddRule(new Rule {
///     Pattern = new Regex(@"/products/\d+"),
///     Callback = async response => {
///         // Parse product page
///         return new[] { new ProductItem { ... } };
///     },
///     Follow = false // Don't follow links from product pages
/// });
/// spider.AddRule(new Rule {
///     Pattern = new Regex(@"/category/.*"),
///     Callback = _ => Task.FromResult(Enumerable.Empty&lt;EntityBase&lt;EntityBase&lt;object&gt;&gt;&gt;()),
///     Follow = true // Follow links from category pages
/// });
/// </code>
/// </example>
public abstract class CrawlSpider : Spider, ICrawlSpider
{
    private readonly ILinkExtractor _linkExtractor;
    private const string PendingRequestsKey = "CrawlSpider:PendingRequests";

    /// <summary>
    /// Gets the collection of rules that control crawling behavior.
    /// </summary>
    /// <value>List of rules defining URL matching and processing logic.</value>
    public List<IRule> Rules { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CrawlSpider"/> class.
    /// </summary>
    /// <param name="linkExtractor">The link extractor to use for discovering links.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="linkExtractor"/> is null.</exception>
    protected CrawlSpider(ILinkExtractor linkExtractor)
    {
        ArgumentNullException.ThrowIfNull(linkExtractor);
        _linkExtractor = linkExtractor;
    }

    /// <summary>
    /// Adds a rule to the spider's rule collection.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
    public void AddRule(IRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Rules.Add(rule);
    }

    /// <summary>
    /// Processes a response and extracts data according to the matching rules.
    /// </summary>
    /// <param name="response">The response to process.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method implements the core crawling logic:
    /// </para>
    /// <list type="number">
    /// <item>Finds rules that match the response URL</item>
    /// <item>Executes matching rule callbacks to extract items</item>
    /// <item>Extracts and schedules links if the rule allows following</item>
    /// <item>Stores pending requests in the execution context for the engine to process</item>
    /// </list>
    /// </remarks>
    public override async Task ProcessResponseAsync(
        Response response,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(context);

        string finalUrl = response.FinalUrl ?? string.Empty;
        var matchedRules = Rules.Where(r => r.Pattern.IsMatch(finalUrl)).ToList();

        // Process each matching rule
        foreach (IRule? rule in matchedRules)
        {
            // Execute rule callback to extract items
            IEnumerable<object> items = await rule.Callback(response).ConfigureAwait(false);
            foreach (object item in items)
            {
                // Store extracted items in context for the engine to process
                await StoreExtractedItemAsync(item, context, cancellationToken).ConfigureAwait(false);
            }

            // If rule allows following links, extract and schedule them
            if (rule.Follow)
            {
                await ExtractAndScheduleLinksAsync(response, context, cancellationToken).ConfigureAwait(false);
            }
        }

        // If no rules matched, still extract links if default following is enabled
        if (matchedRules.Count == 0 && ShouldFollowByDefault())
        {
            await ExtractAndScheduleLinksAsync(response, context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Extracts links from the response and schedules them for crawling.
    /// </summary>
    /// <param name="response">The response containing the HTML content.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Links are filtered using the <see cref="Spider.ShouldFollowUrl"/> method
    /// before being scheduled. Scheduled requests are stored in the execution context
    /// state dictionary for the engine to process.
    /// </remarks>
    protected virtual async Task ExtractAndScheduleLinksAsync(
        Response response,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccess || response.Content.ContentType != ContentType.Html)
        {
            return;
        }

        string baseUrl = response.FinalUrl ?? string.Empty;
        string html = response.Content.Content;

        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        // Extract links using the configured link extractor
        IEnumerable<string> links = _linkExtractor.ExtractLinks(html, baseUrl);

        // Filter and schedule links
        foreach (string? link in links.Where(l => ShouldFollowUrl(l, context)))
        {
            var request = new Request(link);
            await ScheduleRequestAsync(request, context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Schedules a request for processing by the spider engine.
    /// </summary>
    /// <param name="request">The request to schedule.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Requests are stored in the execution context state dictionary under the key
    /// "CrawlSpider:PendingRequests" as a queue for the engine to process.
    /// </remarks>
    protected virtual Task ScheduleRequestAsync(
        Request request,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        // Get or create the pending requests queue
        var pendingRequests = context.State.GetOrAdd(
            PendingRequestsKey,
            _ => new System.Collections.Concurrent.ConcurrentQueue<Request>()
        ) as System.Collections.Concurrent.ConcurrentQueue<Request>;

        pendingRequests?.Enqueue(request);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stores an extracted item in the execution context for processing.
    /// </summary>
    /// <param name="item">The extracted item.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Items are stored in the execution context and the items extracted counter is incremented.
    /// The engine is responsible for further processing (e.g., pipelines, storage).
    /// </remarks>
    protected virtual Task StoreExtractedItemAsync(
        object item,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        // Increment the items extracted counter
        context.IncrementItemsExtracted();

        // Store the item in context state for the engine to process
        // Engine implementations should check for this key and process items accordingly
        const string itemsKey = "CrawlSpider:ExtractedItems";
        var items = context.State.GetOrAdd(
            itemsKey,
            _ => new System.Collections.Concurrent.ConcurrentBag<object>()
        ) as System.Collections.Concurrent.ConcurrentBag<object>;

        items?.Add(item);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines whether to follow links by default when no rules match.
    /// </summary>
    /// <returns><c>true</c> to follow links by default; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Override this method to customize the default link following behavior.
    /// By default, returns <c>false</c> - only follow links when rules explicitly allow it.
    /// </remarks>
    protected virtual bool ShouldFollowByDefault()
    {
        return false;
    }

    /// <summary>
    /// Gets the pending requests that have been scheduled by this spider.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <returns>Collection of pending requests, or empty if none are scheduled.</returns>
    /// <remarks>
    /// This method is typically called by the spider engine to retrieve requests
    /// that the CrawlSpider has discovered and scheduled for processing.
    /// </remarks>
    public virtual IEnumerable<Request> GetPendingRequests(ExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.State.TryGetValue(PendingRequestsKey, out object? queueObj))
        {
            return Enumerable.Empty<Request>();
        }

        if (queueObj is not System.Collections.Concurrent.ConcurrentQueue<Request> queue)
        {
            return Enumerable.Empty<Request>();
        }

        var requests = new List<Request>();
        while (queue.TryDequeue(out Request? request))
        {
            requests.Add(request);
        }

        return requests;
    }

    /// <summary>
    /// Gets the extracted items that have been collected by this spider.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <returns>Collection of extracted items, or empty if none have been extracted.</returns>
    /// <remarks>
    /// This method is typically called by the spider engine to retrieve items
    /// that the CrawlSpider has extracted for further processing.
    /// </remarks>
    public virtual IEnumerable<object> GetExtractedItems(ExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        const string itemsKey = "CrawlSpider:ExtractedItems";
        if (!context.State.TryGetValue(itemsKey, out object? itemsObj))
        {
            return Enumerable.Empty<object>();
        }

        if (itemsObj is not System.Collections.Concurrent.ConcurrentBag<object> items)
        {
            return Enumerable.Empty<object>();
        }

        return items.ToList();
    }
}
