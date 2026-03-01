namespace Ghost.Contracts.Jobs;

/// <summary>
/// Represents the result of a job search operation with optional error information.
/// </summary>
public sealed record JobSearchResult
{
    /// <summary>
    /// List of job listings found during the search.
    /// </summary>
    public IReadOnlyList<JobListing> Jobs { get; init; } = [];

    /// <summary>
    /// Indicates whether the search completed successfully.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error information for each platform that failed during the search.
    /// </summary>
    public IReadOnlyList<PlatformError> PlatformErrors { get; init; } = [];

    /// <summary>
    /// Overall error message if the search failed completely.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional metadata about the search operation.
    /// </summary>
    public SearchMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Represents error information for a specific platform during job search.
/// </summary>
public sealed record PlatformError
{
    /// <summary>
    /// Name of the platform that encountered the error.
    /// </summary>
    public string Platform { get; init; } = string.Empty;

    /// <summary>
    /// Category of the error (Auth, Network, Parse, RateLimit, etc.).
    /// </summary>
    public string ErrorCategory { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Technical details about the error.
    /// </summary>
    public string? TechnicalDetails { get; init; }

    /// <summary>
    /// Suggested action to resolve the error.
    /// </summary>
    public string? Suggestion { get; init; }

    /// <summary>
    /// Whether this error should trigger a retry.
    /// </summary>
    public bool Retryable { get; init; }

    /// <summary>
    /// Timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Metadata about the job search operation.
/// </summary>
public sealed record SearchMetadata
{
    /// <summary>
    /// Total number of platforms that were attempted.
    /// </summary>
    public int TotalPlatforms { get; init; }

    /// <summary>
    /// Number of platforms that succeeded.
    /// </summary>
    public int SuccessfulPlatforms { get; init; }

    /// <summary>
    /// Number of platforms that failed.
    /// </summary>
    public int FailedPlatforms { get; init; }

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>
    /// Search criteria used for this operation.
    /// </summary>
    public JobSearchCriteria? Criteria { get; init; }
}
