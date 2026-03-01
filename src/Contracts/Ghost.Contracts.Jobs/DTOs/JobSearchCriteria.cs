using System.Text.Json.Serialization;

namespace Ghost.Contracts.Jobs;

// Backing field required for Query property fallback logic (Query returns Keywords when _query is null).
// IDE0032 (Use auto property) is suppressed because auto-properties cannot implement this fallback pattern.

public enum TimePosted
{
    Any,
    Past24Hours,
    PastWeek,
    PastMonth
}

/// <summary>
/// Criteria used to search for jobs.
/// </summary>
public sealed record JobSearchCriteria
{
    // Query maps to the same backing storage as Keywords. This design lets
    // callers refer to either 'query' or 'keywords' while keeping a single
    // underlying value. Using an auto-property with a separate Keywords value
    // would complicate JSON deserialization and mapping semantics.
    // Implemented as a forwarding property to avoid introducing an explicit
    // backing field and to satisfy analyzer rules.
    [JsonPropertyName("query")]
    public string? Query
    {
        get => Keywords;
        init => Keywords = value;
    }

    /// <summary>
    /// Text query matching title, company, or description.
    /// </summary>
    // Query property is implemented above as a forwarding property to Keywords.

    /// <summary>
    /// Alternative name for Query field (accepts 'keywords' in JSON).
    /// </summary>
    [JsonPropertyName("keywords")]
    public string? Keywords { get; init; }

    /// <summary>
    /// Location filter.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Desired job type.
    /// </summary>
    public JobType? JobType { get; init; }

    /// <summary>
    /// Desired experience level.
    /// </summary>
    public ExperienceLevel? ExperienceLevel { get; init; }

    /// <summary>
    /// Only remote roles when true.
    /// </summary>
    public bool RemoteOnly { get; init; }

    /// <summary>
    /// Filter by how recently the job was posted.
    /// </summary>
    public TimePosted PostedDate { get; init; } = TimePosted.Any;

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int MaxResults { get; init; } = 25;

    /// <summary>
    /// Optional list of scraper platform names to restrict the search to. Case-insensitive.
    /// When null or empty, all scrapers are used.
    /// </summary>
    public List<string>? Sources { get; init; }

    /// <summary>
    /// Optional scraping strategy override. When provided, the platform client may
    /// attempt to parse this value into a JobScrapingStrategy enum to control
    /// how results are retrieved (e.g. GuestApi, BrowserPage, Hybrid).
    /// </summary>
    public string? Strategy { get; init; }
}
