namespace Ghost.Sdk.Spiders;

/// <summary>
/// Spider that follows links automatically based on configurable rules.
/// </summary>
/// <remarks>
/// CrawlSpiders enable automatic link following by defining rules that specify:
/// <list type="bullet">
/// <item>Which URLs to follow and parse</item>
/// <item>How to extract data from matched URLs</item>
/// <item>Which parsing action to apply to matched URLs</item>
/// </list>
/// </remarks>
public interface ICrawlSpider : ISpider
{
    /// <summary>
    /// Gets the collection of crawl rules.
    /// </summary>
    /// <value>List of rules that control crawling behavior.</value>
    List<CrawlRule> Rules { get; }

    /// <summary>
    /// Adds a crawl rule to the spider.
    /// </summary>
    /// <param name="name">A descriptive name for the rule.</param>
    /// <param name="followCondition">A function that determines whether to follow a URL.</param>
    /// <param name="parseAction">A function that extracts items from a response.</param>
    /// <remarks>
    /// Rules are evaluated in the order they are added. When a URL matches a rule's
    /// followCondition, the parseAction is invoked to extract items from the response.
    /// </remarks>
    void AddRule(string name, Func<string, bool> followCondition, Func<Response, IEnumerable<Item>> parseAction);
}
