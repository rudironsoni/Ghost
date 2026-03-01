namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for navigation and link following.
/// </summary>
public sealed class NavigationConfiguration
{
    /// <summary>
    /// Gets or sets whether to follow links automatically.
    /// </summary>
    public bool FollowLinks { get; set; } = true;

    /// <summary>
    /// Gets or sets the link selector (CSS or XPath).
    /// </summary>
    public string LinkSelector { get; set; } = "a[href]";

    /// <summary>
    /// Gets or sets the selector type for link selector.
    /// </summary>
    public string LinkSelectorType { get; set; } = "CSS";

    /// <summary>
    /// Gets or sets whether to handle pagination automatically.
    /// </summary>
    public bool HandlePagination { get; set; }

    /// <summary>
    /// Gets or sets the pagination configuration.
    /// </summary>
    public PaginationConfiguration? Pagination { get; set; }

    /// <summary>
    /// Gets or sets whether to handle infinite scroll.
    /// </summary>
    public bool HandleInfiniteScroll { get; set; }

    /// <summary>
    /// Gets or sets infinite scroll configuration.
    /// </summary>
    public InfiniteScrollConfiguration? InfiniteScroll { get; set; }

    /// <summary>
    /// Gets or sets delay between page loads (milliseconds).
    /// </summary>
    public int DelayBetweenRequests { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to deduplicate URLs.
    /// </summary>
    public bool DeduplicateUrls { get; set; } = true;
}

/// <summary>
/// Configuration for pagination handling.
/// </summary>
public sealed class PaginationConfiguration
{
    /// <summary>
    /// Gets or sets the pagination type (NextButton, UrlParameter, LoadMore).
    /// </summary>
    public string Type { get; set; } = "NextButton";

    /// <summary>
    /// Gets or sets the selector for the next page button.
    /// </summary>
    public string? NextButtonSelector { get; set; }

    /// <summary>
    /// Gets or sets the URL parameter name for page number.
    /// </summary>
    public string? UrlParameter { get; set; }

    /// <summary>
    /// Gets or sets the starting page number.
    /// </summary>
    public int StartPage { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of pages to crawl.
    /// </summary>
    public int? MaxPages { get; set; }

    /// <summary>
    /// Gets or sets the selector to detect when no more pages are available.
    /// </summary>
    public string? EndConditionSelector { get; set; }
}

/// <summary>
/// Configuration for infinite scroll handling.
/// </summary>
public sealed class InfiniteScrollConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of scrolls.
    /// </summary>
    public int MaxScrolls { get; set; } = 10;

    /// <summary>
    /// Gets or sets delay between scrolls (milliseconds).
    /// </summary>
    public int ScrollDelay { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the scroll step (pixels or "page").
    /// </summary>
    public string ScrollStep { get; set; } = "page";

    /// <summary>
    /// Gets or sets the selector to wait for after each scroll.
    /// </summary>
    public string? WaitForSelector { get; set; }
}
