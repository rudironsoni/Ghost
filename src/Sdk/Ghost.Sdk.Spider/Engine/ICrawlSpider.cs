using System.Text.RegularExpressions;
using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Engine;

/// <summary>
/// Defines a spider that follows links automatically based on rules.
/// </summary>
/// <remarks>
/// CrawlSpiders enable automatic link following by defining rules that specify:
/// - Which URLs to process (via regex patterns)
/// - How to extract data from matched URLs (via callbacks)
/// - Whether to follow links discovered on those pages
/// </remarks>
public interface ICrawlSpider
{
    /// <summary>
    /// Gets the collection of rules that control crawling behavior.
    /// </summary>
    /// <value>List of rules defining URL matching and processing logic.</value>
    public List<IRule> Rules { get; }

    /// <summary>
    /// Adds a rule to the spider's rule collection.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    public void AddRule(IRule rule);
}

/// <summary>
/// Defines a rule for matching URLs and processing responses.
/// </summary>
/// <remarks>
/// Rules are evaluated in order for each response. When a URL matches a rule's pattern,
/// the rule's callback is invoked to extract items, and links are followed if Follow is true.
/// </remarks>
public interface IRule
{
    /// <summary>
    /// Gets the regex pattern for matching URLs.
    /// </summary>
    /// <value>Regular expression to match against URLs.</value>
    public Regex Pattern { get; }

    /// <summary>
    /// Gets the callback function to execute when a URL matches the pattern.
    /// </summary>
    /// <value>Async function that extracts items from the response.</value>
    public Func<Response, Task<IEnumerable<object>>> Callback { get; }

    /// <summary>
    /// Gets a value indicating whether to follow links discovered on pages matching this rule.
    /// </summary>
    /// <value><c>true</c> to extract and follow links; <c>false</c> to stop crawling at this page.</value>
    public bool Follow { get; }
}
