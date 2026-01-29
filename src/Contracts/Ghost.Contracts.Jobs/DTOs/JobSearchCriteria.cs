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
    /// <summary>
    /// Text query matching title, company, or description.
    /// </summary>
    public string? Query { get; init; }

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
}
