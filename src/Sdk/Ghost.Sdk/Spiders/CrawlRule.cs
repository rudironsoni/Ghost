namespace Ghost.Sdk.Spiders;

/// <summary>
/// Represents a rule for matching URLs and processing responses in a CrawlSpider.
/// </summary>
/// <remarks>
/// A CrawlRule defines:
/// <list type="bullet">
/// <item>A condition for determining which URLs to follow</item>
/// <item>An action for extracting items from responses that match the condition</item>
/// </list>
/// Rules are evaluated in order, and multiple rules can match the same URL.
/// </remarks>
public class CrawlRule
{
    /// <summary>
    /// Gets or sets the name of this rule.
    /// </summary>
    /// <value>A descriptive name that identifies this rule.</value>
    /// <remarks>
    /// The name is used for logging and debugging purposes to identify which rule
    /// matched a URL or extracted items.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the condition that determines whether to follow a URL.
    /// </summary>
    /// <value>A function that takes a URL and returns true if it should be followed.</value>
    /// <remarks>
    /// The follow condition is evaluated for each discovered link. If it returns true,
    /// the link will be scheduled for crawling and the parse action will be invoked
    /// when the response is received.
    /// </remarks>
    public Func<string, bool> FollowCondition { get; set; } = _ => false;

    /// <summary>
    /// Gets or sets the action that extracts items from a response.
    /// </summary>
    /// <value>A function that takes a response and returns extracted items.</value>
    /// <remarks>
    /// The parse action is invoked when a response URL matches the follow condition.
    /// It should extract and return all relevant items from the response. If no items
    /// can be extracted, it should return an empty collection.
    /// </remarks>
    public Func<Response, IEnumerable<Item>> ParseAction { get; set; } = _ => Enumerable.Empty<Item>();
}
