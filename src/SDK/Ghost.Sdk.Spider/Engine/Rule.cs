using System.Text.RegularExpressions;
using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Engine;

/// <summary>
/// Default implementation of <see cref="IRule"/> for URL matching and response processing.
/// </summary>
/// <remarks>
/// Rules define the crawling behavior by specifying:
/// <list type="bullet">
/// <item>Pattern: Which URLs to match (regex)</item>
/// <item>Callback: How to process matched URLs and extract items</item>
/// <item>Follow: Whether to follow links found on matched pages</item>
/// </list>
/// </remarks>
public class Rule : IRule
{
    /// <summary>
    /// Gets or sets the regex pattern for matching URLs.
    /// </summary>
    /// <value>
    /// Regular expression to match against URLs. Defaults to ".*" (matches everything).
    /// </value>
    public Regex Pattern { get; set; } = new(".*", RegexOptions.Compiled);

    /// <summary>
    /// Gets or sets the callback function to execute when a URL matches the pattern.
    /// </summary>
    /// <value>
    /// Async function that takes a Response and returns extracted items.
    /// Defaults to a function that returns an empty collection.
    /// </value>
    public Func<Response, Task<IEnumerable<object>>> Callback { get; set; } =
        _ => Task.FromResult(Enumerable.Empty<object>());

    /// <summary>
    /// Gets or sets a value indicating whether to follow links discovered on pages matching this rule.
    /// </summary>
    /// <value>
    /// <c>true</c> to extract and schedule links from matched pages; <c>false</c> to stop crawling at this page.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool Follow { get; set; } = true;
}
