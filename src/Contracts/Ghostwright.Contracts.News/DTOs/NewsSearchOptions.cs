namespace Ghostwright.Contracts.News;

/// <summary>
/// Optional parameters to customize news search behavior.
/// </summary>
public sealed record NewsSearchOptions
{
    /// <summary>
    /// Maximum number of results to return. Defaults to 25.
    /// </summary>
    public int MaxResults { get; init; } = 25;

    /// <summary>
    /// Optional sort order, e.g. "relevance" or "publishedAt".
    /// </summary>
    public string? SortBy { get; init; }
}
