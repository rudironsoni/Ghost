using System;

namespace Ghost.Contracts.News;

/// <summary>
/// Filter used when listing or querying news articles.
/// </summary>
public sealed record NewsFilter
{
    /// <summary>
    /// Restrict to a specific category.
    /// </summary>
    public NewsCategory? Category { get; init; }

    /// <summary>
    /// Only articles published on or after this date.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Only articles published on or before this date.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Source/publisher to filter by.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int MaxResults { get; init; } = 25;
}
