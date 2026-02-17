namespace Ghost.LinkedInScraperHarness.Configuration;

/// <summary>
/// Configuration options for the LinkedIn scraper harness.
/// </summary>
public sealed class ScraperHarnessOptions
{
    /// <summary>
    /// Gets or sets the search keywords to use for testing.
    /// </summary>
    public string SearchKeywords { get; set; } = "Software Engineer";

    /// <summary>
    /// Gets or sets the location to use for testing.
    /// </summary>
    public string Location { get; set; } = "Remote";

    /// <summary>
    /// Gets or sets the maximum number of results to fetch.
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether to fetch detailed job information.
    /// </summary>
    public bool FetchDetails { get; set; } = true;

    /// <summary>
    /// Gets or sets the output format for results (json, table, or csv).
    /// </summary>
    public string OutputFormat { get; set; } = "table";

    /// <summary>
    /// Gets or sets a value indicating whether to run in interactive mode.
    /// </summary>
    public bool InteractiveMode { get; set; }
}
