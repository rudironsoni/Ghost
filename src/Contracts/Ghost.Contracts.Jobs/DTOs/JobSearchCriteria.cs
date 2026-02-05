using System.Text.Json.Serialization;

namespace Ghost.Contracts.Jobs;

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
#pragma warning disable IDE0032 // Use auto property - backing field is required for fallback logic
    private string? _query;
#pragma warning restore IDE0032
    
    /// <summary>
    /// Text query matching title, company, or description.
    /// </summary>
    [JsonPropertyName("query")]
    public string? Query
    {
        get => _query ?? Keywords;
        init => _query = value;
    }
    
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
